using UnityEngine;
using Photon.Pun;
using RetroDodge.AI;

namespace RetroDodge
{
	public class AISessionConfig : ScriptableObject
	{
		[Header("Session Flags")]
		public bool withAI = false;
		public AIDifficulty difficulty = AIDifficulty.Normal;
		[Tooltip("If -1, pick random at runtime")] public int aiCharacterIndex = -1;
		[Tooltip("If -1, use player's current selection or default 0")] public int playerCharacterIndex = -1;

		[Header("Bootstrap Scene Names")] public string gameplaySceneName = "GameplayArena";

		private static AISessionConfig instance;
		public static AISessionConfig Instance
		{
			get
			{
				if (instance == null)
				{
					instance = Resources.Load<AISessionConfig>("AISessionConfig");
					if (instance == null)
					{
						instance = CreateInstance<AISessionConfig>();
					}
				}
				return instance;
			}
		}

		public void SetPlayWithAI(AIDifficulty selectedDifficulty, int playerIndex = -1, int aiIndex = -1)
		{
			withAI = true;
			difficulty = selectedDifficulty;
			playerCharacterIndex = playerIndex;
			aiCharacterIndex = aiIndex;
		}

		public void Clear()
		{
			withAI = false;
			aiCharacterIndex = -1;
			playerCharacterIndex = -1;
		}
	}
}


