namespace GuitarPlatformer
{
    /// <summary>
    /// Hands a level from the Level Creator into Play Mode. The editor stores the JSON
    /// in SessionState as well, because domain reload wipes this static on enter-play.
    /// </summary>
    public static class CustomLevelSession
    {
        private static RiffLevel pending;

        public static bool HasPending => pending != null;

        public static void Queue(RiffLevel level)
        {
            pending = level;
        }

        public static RiffLevel Consume()
        {
            RiffLevel level = pending;
            pending = null;
            return level;
        }
    }
}
