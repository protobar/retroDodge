using UnityEngine;

namespace RetroDodge.AI
{
	public enum AIDifficulty
	{
		Easy,
		Normal,
		Hard
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
						reactionDelaySeconds = 0.1f,
						aimInaccuracyDegrees = 8f,
						throwDecisionCooldown = 0.7f,
						dodgeProbability = 0.3f,
						jumpProbability = 0.12f, // Still reduced from original
						aggression = 0.6f,
						mistakeChance = 0.05f, // Fewer mistakes
						reactionSpeed = 1.2f,
						maxActionsPerSecond = 2.5f
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


