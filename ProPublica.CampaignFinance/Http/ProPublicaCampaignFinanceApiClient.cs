using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Flurl.Http;
using Flurl.Http.Configuration;
using Newtonsoft.Json.Linq;

namespace ProPublica.CampaignFinance.Http
{
    public class ProPublicaCampaignFinanceApiClient : IDisposable
    {
        private readonly IFlurlClient _http;

        private readonly SemaphoreSlim _requests = new SemaphoreSlim(2, 2);

        private bool _disposed;

        public ProPublicaCampaignFinanceApiClient(IFlurlClient http)
        {
            _http = http;
        }

        public ProPublicaCampaignFinanceApiClient(IFlurlClientFactory http, string key)
            : this(http.Get("https://api.propublica.org/campaign-finance/v1/").WithHeader("X-API-KEY", key))
        {
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~ProPublicaCampaignFinanceApiClient()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;
            if (disposing)
                _requests.Dispose();
            _disposed = true;
        }

        public Task<ClientResponse> GetAsync(string requestUri, Action<ClientRequestBuilder> requestBuilderSetup = null)
        {
            var requestBuilder = new ClientRequestBuilder(requestUri);

            requestBuilderSetup?.Invoke(requestBuilder);

            var request = requestBuilder.GetRequest();

            return GetAsync(request);
        }

        private async Task<ClientResponse> GetAsync(ClientRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await _requests.WaitAsync(cancellationToken);

                while (true)
                {
                    using var response = await _http.Request(request.Path).SetQueryParams(request.Query)
                        .AllowAnyHttpStatus().GetAsync(cancellationToken);

                    switch ((HttpStatusCode) response.StatusCode)
                    {
                        case HttpStatusCode.OK:
                            break;
                        case HttpStatusCode.BadRequest:
                        case HttpStatusCode.Forbidden:
                        case HttpStatusCode.NotFound:
                        case HttpStatusCode.NotAcceptable:
                            throw new InvalidOperationException(response.StatusCode.ToString());
                        case HttpStatusCode.TooManyRequests:
                            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                            continue;
                        case HttpStatusCode.InternalServerError:
                        case HttpStatusCode.GatewayTimeout:
                            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                            continue;
                        //throw new InvalidOperationException($"Gateway timeout! ({response.RequestMessage.RequestUri})");
                        case HttpStatusCode.ServiceUnavailable:
                            throw new InvalidOperationException("Service is unavailable.");
                        default:
                            throw new InvalidOperationException(
                                $"Something unexpected happened. ({response.StatusCode})");
                    }

                    var content = await response.GetStringAsync();
                    var results = new ClientResponse(JObject.Parse(content));

                    var message = results.GetMessage();
                    if (message != null)
                        throw new InvalidOperationException(message);

                    var status = results.GetStatus();

                    return status switch
                    {
                        ClientResponseStatus.Ok => results,
                        ClientResponseStatus.Error => throw new InvalidOperationException(
                            string.Join(Environment.NewLine, results.GetErrors())),
                        ClientResponseStatus.InternalServerError => throw new InvalidOperationException(
                            "Internal server error!"),
                        _ => throw new ArgumentOutOfRangeException($"Invalid response status: {status}.")
                    };
                }
            }
            finally
            {
                _requests.Release();
            }
        }

        #region Lobbyist Bundlers

        public async Task<IEnumerable<LobbyistBundler>> GetLobbyistBundlersByCommittee(int cycle, string fecId)
        {
            var response = await GetAsync($"{cycle}/committees/{fecId}/lobbyist_bundlers.json");
            return response.Many<LobbyistBundler>();
        }

        #endregion

        #region Candidates

        public async Task<IEnumerable<CandidateSearchResult>> GetCandidates(int cycle, string query = null)
        {
            var response = await GetAsync($"{cycle}/candidates/search.json", options => options.Query["query"] = query);
            return response.Many<CandidateSearchResult>();
        }

        public async Task<IEnumerable<CandidateFromAState>> GetCandidatesFromState(int cycle, string state)
        {
            var response = await GetAsync($"{cycle}/races/{state}.json");
            return response.Many<CandidateFromAState>();
        }

        public async Task<IEnumerable<CandidateFromAState>> GetCandidatesFromState(int cycle, string state,
            string chamber)
        {
            // house or senate
            var response = await GetAsync($"{cycle}/races/{state}/{chamber}.json");
            return response.Many<CandidateFromAState>();
        }

        public async Task<IEnumerable<CandidateFromAState>> GetCandidatesFromState(int cycle, string state,
            string chamber, int district)
        {
            // house or senate
            // don’t include for states with a single representative (AL, DE, DC, MT, ND, SD, VT). (house requests only - districts with senate requests will be ignored.)
            var response = await GetAsync($"{cycle}/races/{state}/{chamber}/{district}.json");
            return response.Many<CandidateFromAState>();
        }

        public async Task<Candidate> GetCandidate(int cycle, string fecId)
        {
            var response = await GetAsync($"{cycle}/candidates/{fecId}");
            return response.One<Candidate>();
        }

        public async Task<IEnumerable<LateContribution>> GetRecentLateContributions(int cycle)
        {
            var response = await GetAsync($"{cycle}/contributions/48hour.json");
            return response.Many<LateContribution>();
        }

        public async Task<IEnumerable<LateContribution>> GetRecentLateContributionsToCandidate(int cycle, string fecId)
        {
            var response = await GetAsync($"{cycle}/candidates/{fecId}/48hour.json");
            return response.Many<LateContribution>();
        }

        public async Task<IEnumerable<LateContribution>> GetRecentLateContributionsToCommittee(int cycle, string fecId)
        {
            var response = await GetAsync($"{cycle}/committees/{fecId}/48hour.json");
            return response.Many<LateContribution>();
        }

        public async Task<IEnumerable<LateContribution>> GetRecentLateContributionsByDate(int cycle, int year,
            int month, int day)
        {
            var response = await GetAsync($"{cycle}/contributions/48hour/{year}/{month}/{day}.json");
            return response.Many<LateContribution>();
        }

        public Task<IEnumerable<LateContribution>> GetRecentLateContributionsByDate(int cycle, DateTime date)
        {
            return GetRecentLateContributionsByDate(cycle, date.Year, date.Month, date.Day);
        }

        #endregion

        #region Committees

        public async Task<IEnumerable<CommitteeSearchResult>> GetCommittees(int cycle, string query)
        {
            var response = await GetAsync($"{cycle}/committees/search.json", options => options.Query["query"] = query);
            return response.Many<CommitteeSearchResult>();
        }

        public async Task<Committee> GetCommittee(int cycle, string fecId)
        {
            var response = await GetAsync($"{cycle}/committees/{fecId}.json");
            return response.One<Committee>();
        }

        public async Task<IEnumerable<CommitteeFiling>> GetCommitteeFilings(int cycle, string fecId)
        {
            var response = await GetAsync($"{cycle}/committees/{fecId}/filings.json");
            return response.Many<CommitteeFiling>();
        }

        #endregion

        #region Filings

        public async Task<IEnumerable<ElectronicFilingFormType>> GetElectronicFilingFormTypes(int cycle)
        {
            var response = await GetAsync($"{cycle}/filings/types.json");
            return response.Many<ElectronicFilingFormType>();
        }

        public async Task<IEnumerable<ElectronicFiling>> GetElectronicFilings(int cycle, string query)
        {
            var response = await GetAsync($"{cycle}/filings/search.json", options => options.Query["query"] = query);
            return response.Many<ElectronicFiling>();
        }

        public async Task<IEnumerable<ElectronicFiling>> GetElectronicFilingsByDate(int cycle, int year, int month,
            int day)
        {
            var response = await GetAsync($"{cycle}/filings/{year}/{month}/{day}.json");
            return response.Many<ElectronicFiling>();
        }

        public Task<IEnumerable<ElectronicFiling>> GetElectronicFilingsByDate(int cycle, DateTime date)
        {
            return GetElectronicFilingsByDate(cycle, date.Year, date.Month, date.Day);
        }

        public async Task<IEnumerable<ElectronicFiling>> GetElectronicFilingsByType(int cycle, string type)
        {
            var response = await GetAsync($"{cycle}/filings/types/{type}.json");
            return response.Many<ElectronicFiling>();
        }

        public async Task<PresidentialElectronicFiling> GetElectronicFiling(int cycle, string type)
        {
            var response = await GetAsync($"{cycle}/filings/{type}.json");
            return response.One<PresidentialElectronicFiling>();
        }

        #endregion

        #region Independent Spending

        public async Task<IEnumerable<IndependentExpenditure>> GetIndependentExpenditures(int cycle)
        {
            var response = await GetAsync($"{cycle}/independent_expenditures.json");
            return response.Many<IndependentExpenditure>();
        }

        public async Task<IEnumerable<IndependentExpenditure>> GetIndependentExpendituresByDate(int cycle, int year,
            int month, int day)
        {
            var response = await GetAsync($"{cycle}/independent_expenditures/{year}/{month}/{day}.json");
            return response.Many<IndependentExpenditure>();
        }

        public Task<IEnumerable<IndependentExpenditure>> GetIndependentExpendituresByDate(int cycle, DateTime date)
        {
            return GetIndependentExpendituresByDate(cycle, date.Year, date.Month, date.Day);
        }

        public async Task<IEnumerable<IndependentExpenditure>> GetIndependentExpendituresByCommittee(int cycle,
            string fedId)
        {
            var response = await GetAsync($"{cycle}/committees/{fedId}/independent_expenditures.json");
            return response.Many<IndependentExpenditure>();
        }

        public async Task<IEnumerable<IndependentExpenditure>> GetIndependentExpendituresThatSupportOrOpposeCandidate(
            int cycle, string fedId)
        {
            var response = await GetAsync($"{cycle}/candidates/{fedId}/independent_expenditures.json");
            return response.Many<IndependentExpenditure>();
        }

        public async Task<IEnumerable<IndependentExpenditureRaceTotal>> GetIndependentExpenditureRaceTotalsForOffice(
            int cycle, string office)
        {
            // senate, house, president
            var response = await GetAsync($"{cycle}/independent_expenditures/race_totals/{office}.json");
            return response.Many<IndependentExpenditureRaceTotal>();
        }

        public async Task<IEnumerable<IndependentExpenditureRaceTotal>> GetIndependentExpenditureRaceTotalsForCommittee(
            int cycle, string fecId)
        {
            var response = await GetAsync($"{cycle}/committees/{fecId}/independent_expenditures/races.json");
            return response.Many<IndependentExpenditureRaceTotal>();
        }

        #endregion

        #region Electioneering Communications

        public async Task<IEnumerable<ElectioneeringCommunication>> GetElectioneeringCommunications(int cycle)
        {
            var response = await GetAsync($"{cycle}/electioneering_communications.json");
            return response.Many<ElectioneeringCommunication>();
        }

        public async Task<IEnumerable<ElectioneeringCommunication>> GetElectioneeringCommunicationsByCommittee(
            int cycle, string fecId)
        {
            var response = await GetAsync($"{cycle}/committees/{fecId}/electioneering_communications.json");
            return response.Many<ElectioneeringCommunication>();
        }

        public async Task<IEnumerable<ElectioneeringCommunication>> GetElectioneeringCommunicationsByDate(int cycle,
            int year, int month, int day)
        {
            var response = await GetAsync($"{cycle}/electioneering_communications/{year}/{month}/{day}.json.json");
            return response.Many<ElectioneeringCommunication>();
        }

        public Task<IEnumerable<ElectioneeringCommunication>> GetElectioneeringCommunicationsByDate(int cycle,
            DateTime date)
        {
            return GetElectioneeringCommunicationsByDate(cycle, date.Year, date.Month, date.Day);
        }

        #endregion
    }
}