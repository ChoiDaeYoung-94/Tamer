// TEST CANDIDATE ONLY: append after account-deletion.js in the preserved revision.
// Never publish this gate or its private configuration to a production title.
// Keep tamerDeletionConfig.enabled false until the complete candidate is reviewed.
(function () {
    var key = "TamerDeletionDisposableTestV1";
    var config = handlers.getCurrentPlayerDeletionConfigV1;
    var deletion = handlers.requestCurrentPlayerDeletionV1;
    if (typeof config !== "function" || typeof deletion !== "function")
        throw new Error("Deletion handlers must precede the test gate");

    function permitted() {
        try {
            var response = server.GetTitleInternalData({ Keys: [key] });
            var value = response && response.Data && response.Data[key];
            if (typeof value !== "string") return false;
            var rule = JSON.parse(value);
            var now = Date.now();
            return rule && rule.enabled === true &&
                typeof rule.titleId === "string" && rule.titleId === script.titleId &&
                rule.titleId === tamerDeletionConfig.titleId &&
                typeof rule.playerId === "string" && rule.playerId === currentPlayerId &&
                typeof rule.startsAt === "number" && typeof rule.expiresAt === "number" &&
                isFinite(rule.startsAt) && isFinite(rule.expiresAt) &&
                rule.startsAt <= now && now < rule.expiresAt &&
                rule.expiresAt - rule.startsAt > 0 && rule.expiresAt - rule.startsAt <= 900000;
        } catch (error) { return false; }
    }
    handlers.getCurrentPlayerDeletionConfigV1 = function () {
        return permitted() ? config() : { available: false, protocol: "tamer-title-deletion-v1" };
    };
    handlers.requestCurrentPlayerDeletionV1 = function (args) {
        return permitted() ? deletion(args) : { accepted: false, protocol: "tamer-title-deletion-v1" };
    };
}());
