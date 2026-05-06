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

    [Header("Slingshot Bonus")]
    [Tooltip("Flat bonus awarded for completing a Mars slingshot (ship entered the Mars gravity range and exited it without dying).")]
    public float slingshotBonusMax = 1500f;

    [Tooltip("Extra bonus for staying close to the predicted slingshot path. Scales linearly from full at 0 average deviation to 0 when average deviation reaches slingshotPathTolerance.")]
    public float slingshotPrecisionBonusMax = 2000f;

    [Tooltip("Average deviation in world units at which the precision bonus reaches 0. Smaller = stricter / harder to earn full precision.")]
    public float slingshotPathTolerance = 8f;

    [Header("Clamp")]
    [Tooltip("Minimum score the player can end with.")]
    public float minScore = 0f;

    [Tooltip("If true, dying forces the score to minScore regardless of earned bonuses.")]
    public bool zeroScoreOnDeath = false;
}
