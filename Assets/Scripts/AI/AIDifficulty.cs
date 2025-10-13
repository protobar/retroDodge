using UnityEngine;

namespace RetroDodge.AI
{
	public enum AIDifficulty
	{
		Easy,
		Normal,
		Hard,
		Nightmare
	}

	[System.Serializable]
	public class AIDifficultyParams
	{
		public float reactionDelaySeconds = 0.15f;
		public float aimInaccuracyDegrees = 12f;
		public float throwDecisionCooldown = 0.9f;
		public float dodgeProbability = 0.25f;
		public float jumpProbability = 0.15f;
		public float aggression = 0.55f; // 0..1: advance vs kite
		
		// New balanced parameters
		public float mistakeChance = 0.1f; // Chance to make suboptimal decisions
		public float reactionSpeed = 1f; // Multiplier for reaction times
		public float maxActionsPerSecond = 2f; // Input rate limiting
	}

	public static class AIDifficultyPresets
	{
		public static AIDifficultyParams Get(AIDifficulty difficulty)
		{
			switch (difficulty)
			{
				case AIDifficulty.Easy:
					return new AIDifficultyParams
					{
						reactionDelaySeconds = 0.4f,
						aimInaccuracyDegrees = 25f,
						throwDecisionCooldown = 1.8f,
						dodgeProbability = 0.1f,
						jumpProbability = 0.06f, // Much less jumping
						aggression = 0.3f,
						mistakeChance = 0.25f, // Makes more mistakes
						reactionSpeed = 0.7f,
						maxActionsPerSecond = 1.2f
					};
				case AIDifficulty.Hard:
					return new AIDifficultyParams
					{
						reactionDelaySeconds = 0.08f, // Faster reactions
						aimInaccuracyDegrees = 5f, // More accurate
						throwDecisionCooldown = 0.5f, // Faster decisions
						dodgeProbability = 0.4f, // More dodging
						jumpProbability = 0.15f, // More jumping
						aggression = 0.75f, // More aggressive
						mistakeChance = 0.02f, // Almost no mistakes
						reactionSpeed = 1.4f, // Faster reactions
						maxActionsPerSecond = 3.0f // More actions
					};
				case AIDifficulty.Nightmare:
					return new AIDifficultyParams
					{
						reactionDelaySeconds = 0.05f, // Lightning fast
						aimInaccuracyDegrees = 2f, // Near perfect aim
						throwDecisionCooldown = 0.3f, // Instant decisions
						dodgeProbability = 0.6f, // Dodges everything
						jumpProbability = 0.25f, // Aggressive jumping
						aggression = 0.9f, // Maximum aggression
						mistakeChance = 0.0f, // No mistakes
						reactionSpeed = 1.8f, // Superhuman reactions
						maxActionsPerSecond = 4.0f // Maximum actions
					};
				default:
					return new AIDifficultyParams
					{
						reactionDelaySeconds = 0.2f,
						aimInaccuracyDegrees = 15f,
						throwDecisionCooldown = 1.1f,
						dodgeProbability = 0.2f,
						jumpProbability = 0.08f, // Much more reasonable
						aggression = 0.45f,
						mistakeChance = 0.15f, // Balanced mistakes
						reactionSpeed = 0.9f,
						maxActionsPerSecond = 1.8f
					};
			}
		}
	}
}


