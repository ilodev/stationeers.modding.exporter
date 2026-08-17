namespace stationeers.modding.exporter
{
    internal sealed class MonoScriptReferenceBundlePatchResult
    {
        public int MatchedMappingCount { get; }

        public int RewrittenScriptTypeCount { get; }

        public int RewrittenMonoBehaviourCount { get; }

        public int TotalRewriteCount =>
            RewrittenScriptTypeCount + RewrittenMonoBehaviourCount;

        public MonoScriptReferenceBundlePatchResult(
            int matchedMappingCount,
            int rewrittenScriptTypeCount,
            int rewrittenMonoBehaviourCount)
        {
            MatchedMappingCount = matchedMappingCount;
            RewrittenScriptTypeCount = rewrittenScriptTypeCount;
            RewrittenMonoBehaviourCount = rewrittenMonoBehaviourCount;
        }

        public static MonoScriptReferenceBundlePatchResult Empty { get; } =
            new MonoScriptReferenceBundlePatchResult(0, 0, 0);
    }
}
