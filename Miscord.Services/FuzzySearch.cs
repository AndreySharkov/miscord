using System;

namespace Miscord.Services
{
    public static class FuzzySearch
    {
        public static double GetJaroWinklerSimilarity(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2)) return 0.0;
            if (s1 == s2) return 1.0;

            s1 = s1.ToLower();
            s2 = s2.ToLower();

            int l1 = s1.Length;
            int l2 = s2.Length;
            int matchDistance = Math.Max(l1, l2) / 2 - 1;

            bool[] s1Matches = new bool[l1];
            bool[] s2Matches = new bool[l2];

            int matches = 0;
            int transpositions = 0;

            // 1. Find matching characters
            for (int i = 0; i < l1; i++)
            {
                int start = Math.Max(0, i - matchDistance);
                int end = Math.Min(i + matchDistance + 1, l2);

                for (int j = start; j < end; j++)
                {
                    if (s2Matches[j]) continue;
                    if (s1[i] != s2[j]) continue;

                    s1Matches[i] = true;
                    s2Matches[j] = true;
                    matches++;
                    break;
                }
            }

            if (matches == 0) return 0.0;

            // 2. Count transpositions
            int k = 0;
            for (int i = 0; i < l1; i++)
            {
                if (!s1Matches[i]) continue;
                while (!s2Matches[k]) k++;

                if (s1[i] != s2[k]) transpositions++;
                k++;
            }

            double m = matches;
            double t = transpositions / 2.0;

            // 3. Jaro Formula
            double jaro = (m / l1 + m / l2 + (m - t) / m) / 3.0;

            // 4. Winkler Modification
            double p = 0.1; // Scaling factor
            int l = 0;      // Prefix length

            for (int i = 0; i < Math.Min(4, Math.Min(l1, l2)); i++)
            {
                if (s1[i] == s2[i]) l++;
                else break;
            }

            return jaro + (l * p * (1.0 - jaro));
        }
    }
}
