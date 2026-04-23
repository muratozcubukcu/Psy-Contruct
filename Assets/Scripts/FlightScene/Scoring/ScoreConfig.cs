using UnityEngine;

/// <summary>
/// Tunable weights and bonus values for the run-scoring formula.
/// Create one via: Assets > Create > Scoring > Score Config
/// </summary>
[CreateAssetMenu(fileName = "ScoreConfig", menuName = "Scoring/Score Config")]
public class ScoreConfig : ScriptableObject {

    [Header("Bonuses")]
    [Tooltip("Max bonus awarded for a fast run. Decays linearly to 0 over timeBonusDurationSeconds.")]
    public float timeBonusMax = 2000f;

    [Tooltip("Seconds after which the time bonus reaches 0.")]
    public float timeBonusDurationSeconds = 200f;

    [Tooltip("Max bonus awarded for preserving fuel (earned at 100% fuel remaining).")]
    public float fuelBonusMax = 3000f;

    [Tooltip("Max bonus awarded for avoiding damage (earned at 100% HP remaining).")]
    public float healthBonusMax = 5000f;

    [Tooltip("Flat bonus added on successful level completion.")]
    public float completionBonus = 0f;

    [Header("Clamp")]
    [Tooltip("Minimum score the player can end with.")]
    public float minScore = 0f;

    [Tooltip("If true, dying forces the score to minScore regardless of earned bonuses.")]
    public bool zeroScoreOnDeath = false;
}
