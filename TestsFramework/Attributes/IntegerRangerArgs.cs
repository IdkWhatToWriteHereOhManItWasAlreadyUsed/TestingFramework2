

namespace TestsFramework.Attributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class IntegerRangesArgs : Attribute
    {
        private readonly List<int[]> _ranges = new();

        public IntegerRangesArgs(params int[] pairs)
        {
            if (pairs.Length % 2 != 0)
                throw new ArgumentException("Нужно четное количество чисел: start,end,start,end,...");

            for (int i = 0; i < pairs.Length; i += 2)
            {
                int start = pairs[i];
                int end = pairs[i + 1];
                int min = Math.Min(start, end);
                int max = Math.Max(start, end);
                _ranges.Add(Enumerable.Range(min, max - min + 1).ToArray());
            }
        }

        public IEnumerable<object[]> GetAllCombinations()
        {
            return CombineAllRanges(_ranges);
        }

        private static IEnumerable<object[]> CombineAllRanges(List<int[]> ranges)
        {
            if (ranges.Count == 0)
                yield break;

            int[] indexes = new int[ranges.Count];
            int[] lengths = ranges.Select(r => r.Length).ToArray();

            while (true)
            {
                object[] combination = new object[ranges.Count];
                for (int i = 0; i < ranges.Count; i++)
                    combination[i] = ranges[i][indexes[i]];
                yield return combination;

                int pos = ranges.Count - 1;
                while (pos >= 0)
                {
                    indexes[pos]++;
                    if (indexes[pos] < lengths[pos])
                        break;
                    indexes[pos] = 0;
                    pos--;
                }
                if (pos < 0)
                    break;
            }
        }
    }
}