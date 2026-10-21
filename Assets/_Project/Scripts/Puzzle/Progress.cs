namespace CarMatch
{
    // Level progression. A win on the saved level moves to exactly the next one; a win on any other
    // level (a replay, a stale or repeated settle) never moves the saved level. Nothing can skip.
    public static class Progress
    {
        public static int AfterWin(int saved, int won, int count)
        {
            if (count < 1) return 0;
            if (saved < 0) saved = 0;
            if (saved > count - 1) saved = count - 1;
            if (won != saved) return saved;
            return saved < count - 1 ? saved + 1 : saved;
        }
    }
}
