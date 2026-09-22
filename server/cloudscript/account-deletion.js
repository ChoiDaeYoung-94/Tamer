// Merge these handlers into a reviewed Classic CloudScript revision; do not replace other handlers.
// Deployment candidate only. Set both values explicitly for an authorized TEST title first.
var tamerDeletionConfig = { enabled: false, titleId: "" };

(function () {
    var protocol = "tamer-title-deletion-v1";
    function available() {
        return tamerDeletionConfig.enabled === true &&
            typeof tamerDeletionConfig.titleId === "string" && tamerDeletionConfig.titleId.length > 0 &&
            script.titleId === tamerDeletionConfig.titleId &&
            typeof currentPlayerId === "string" && /^[A-Za-z0-9-]{1,128}$/.test(currentPlayerId);
    }
    handlers.getCurrentPlayerDeletionConfigV1 = function () {
        return { available: available(), protocol: protocol };
    };
    handlers.requestCurrentPlayerDeletionV1 = function (args) {
        if (!available() || !args || typeof args !== "object" || Array.isArray(args) ||
            Object.keys(args).length !== 2 || args.confirmed !== true ||
            typeof args.requestId !== "string" || !/^[0-9a-f]{32}$/.test(args.requestId)) {
            return { accepted: false, protocol: protocol };
        }
        // Never take a target/title/entity from args. Never call Admin/DeleteMasterPlayerAccount.
        // No retry or status observer: an exception can mean the response was lost after acceptance.
        try {
            var result = server.DeletePlayer({ PlayFabId: currentPlayerId });
            if (!result || typeof result !== "object" || Array.isArray(result) || Object.keys(result).length !== 0)
                return { accepted: false, protocol: protocol };
            return { accepted: true, requestId: args.requestId, scope: "title", protocol: protocol };
        } catch (error) {
            // Do not log/return the raw SDK error, credentials or account data.
            return { accepted: false, protocol: protocol };
        }
    };
}());
