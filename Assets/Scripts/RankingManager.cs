using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class RankingManager : MonoBehaviour
{
    private const int RANK_COUNT = 5;
    private const string PREF_KEY = "RankingData";

    private List<float> ranking = new List<float>();

    void Awake()
    {
        LoadRanking();
    }

    public List<float> UpdateRanking(float newScore)
    {
        ranking.Add(newScore);
        ranking = ranking.OrderByDescending(x => x).Take(RANK_COUNT).ToList();
        SaveRanking();
        return ranking;
    }

    public int GetRank(float score)
    {
        for (int i = 0; i < ranking.Count; i++)
        {
            if (Mathf.Approximately(score, ranking[i])) return i;
        }
        return -1;
    }

    public List<float> GetRanking()
    {
        return ranking;
    }

    private void SaveRanking()
    {
        for (int i = 0; i < ranking.Count; i++)
        {
            PlayerPrefs.SetFloat($"{PREF_KEY}_{i}", ranking[i]);
        }
        PlayerPrefs.Save();
    }

    private void LoadRanking()
    {
        ranking.Clear();
        for (int i = 0; i < RANK_COUNT; i++)
        {
            if (PlayerPrefs.HasKey($"{PREF_KEY}_{i}"))
                ranking.Add(PlayerPrefs.GetFloat($"{PREF_KEY}_{i}"));
            else
                ranking.Add(0f);
        }
    }
}
