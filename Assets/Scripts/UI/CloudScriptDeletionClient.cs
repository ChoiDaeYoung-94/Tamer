using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AD.Privacy;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

namespace AD
{
    public static class CloudScriptDeletionClient
    {
        public const string SupportEmail = "doeud1410@gmail.com";
        public const string AvailabilityFunction = "getCurrentPlayerDeletionConfigV1";
        private const string BindingDomain = "tamer-classic-cloudscript-deletion";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
#if !TAMER_GAMEPLAY_HARNESS && !TAMER_IAP_HARNESS
            // This only installs local composition. No CloudScript runs until the user requests a preview.
            string directory = Path.Combine(Application.persistentDataPath, "CloudScriptDeletion");
            DeletionRecoveryGuard.HasPendingSubmission = account =>
                Store(directory, PlayFabSettings.TitleId, account).Load()?.SubmissionStarted == true;
            DeletionPresenter.RuntimeFlowFactory = () => CreateFlow(directory);
#endif
        }

        private static FileDeletionRecoveryStore Store(string directory, string title, string account)
            => new FileDeletionRecoveryStore(Path.Combine(directory, DeletionRecovery.Hash(BindingDomain, title, account) + ".json"));

        private static DeletionFlow CreateFlow(string directory)
        {
            var owner = Managers.DataM;
            var session = owner?.DeletionSession();
            if (session == null || !session.HasEntityBinding)
                return new DeletionFlow(new UnavailableDeletionGateway(), () => null);
            int revision = 0;
            PlayFabClientInstanceAPI api = null;
            var gateway = new CloudScriptDeletionGateway(async (captured, requestId, token) =>
            {
                ValidateCurrent(owner, captured);
                if (api == null || revision <= 0) throw new InvalidOperationException();
                var result = await Execute(api, new ExecuteCloudScriptRequest
                {
                    FunctionName = CloudScriptDeletionGateway.FunctionName,
                    RevisionSelection = CloudScriptRevisionOption.Specific, SpecificRevision = revision,
                    FunctionParameter = new Dictionary<string, object> { ["confirmed"] = true, ["requestId"] = requestId },
                    GeneratePlayStreamEvent = false
                }, token);
                return IsAccepted(result, requestId, revision);
            }, prepare: async (captured, token) =>
            {
                ValidateCurrent(owner, captured);
                var context = new PlayFabAuthenticationContext();
                context.CopyFrom(PlayFabSettings.staticPlayer);
                api = new PlayFabClientInstanceAPI(new PlayFabApiSettings { TitleId = captured.TitleId }, context);
                var result = await Execute(api, new ExecuteCloudScriptRequest
                {
                    FunctionName = AvailabilityFunction, RevisionSelection = CloudScriptRevisionOption.Live,
                    GeneratePlayStreamEvent = false
                }, token);
                ValidateCurrent(owner, captured);
                if (!IsAvailable(result)) return false;
                revision = result.Revision;
                return true;
            });
            return new DeletionFlow(gateway, () => ReferenceEquals(owner, Managers.DataM) ? owner.DeletionSession() : null,
                owner.BeginDeletionSubmission, owner.FinishAcceptedDeletion, owner.FinishCancelledDeletion,
                Store(directory, session.TitleId, session.AccountId),
                DeletionRecovery.Hash(BindingDomain, session.TitleId, session.AccountId, session.EntityId));
        }

        private static void ValidateCurrent(DataManager owner, DeletionSession captured)
        {
            var player = PlayFabSettings.staticPlayer;
            if (!ReferenceEquals(owner, Managers.DataM) || !captured.Matches(owner.DeletionSession()) ||
                player.PlayFabId != captured.AccountId || player.EntityId != captured.EntityId ||
                player.EntityType != "title_player_account" || PlayFabSettings.TitleId != captured.TitleId ||
                string.IsNullOrEmpty(player.ClientSessionTicket)) throw new InvalidOperationException();
        }

        // No retries. A timeout/cancellation closes this local wait; late responses cannot authorize cleanup.
        private static async Task<ExecuteCloudScriptResult> Execute(PlayFabClientInstanceAPI api, ExecuteCloudScriptRequest request, CancellationToken token)
        {
            var completion = new TaskCompletionSource<ExecuteCloudScriptResult>();
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(20));
                using (timeout.Token.Register(() => completion.TrySetCanceled()))
                {
                    timeout.Token.ThrowIfCancellationRequested();
                    api.ExecuteCloudScript(request, result => completion.TrySetResult(result),
                        error => completion.TrySetException(new InvalidOperationException("CloudScript response unavailable.")));
                    return await completion.Task;
                }
            }
        }

        public static bool IsAvailable(ExecuteCloudScriptResult result)
            => Valid(result, AvailabilityFunction) && result.Revision > 0 &&
                result.FunctionResult is IDictionary<string, object> data && data.Count == 2 &&
                data.TryGetValue("available", out var available) && available is bool flag && flag && ProtocolMatches(data);

        public static bool IsAccepted(ExecuteCloudScriptResult result, string requestId, int revision)
            => Valid(result, CloudScriptDeletionGateway.FunctionName) && result.Revision == revision &&
                result.FunctionResult is IDictionary<string, object> data && data.Count == 4 && ProtocolMatches(data) &&
                data.TryGetValue("accepted", out var accepted) && accepted is bool flag && flag &&
                data.TryGetValue("scope", out var scope) && scope is string text && text == "title" &&
                data.TryGetValue("requestId", out var returned) && returned is string id && id == requestId;

        private static bool ProtocolMatches(IDictionary<string, object> data)
            => data.TryGetValue("protocol", out var protocol) && protocol is string text && text == CloudScriptDeletionGateway.Protocol;
        private static bool Valid(ExecuteCloudScriptResult result, string function)
            => result != null && result.Error == null && result.FunctionResultTooLarge != true && result.FunctionName == function;
    }
}
