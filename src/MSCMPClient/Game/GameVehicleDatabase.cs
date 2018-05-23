using System.Collections.Generic;
using UnityEngine;
using MSCMP.Game.Objects;
using MSCMP.Game.Components;

namespace MSCMP.Game {
	/// <summary>
	/// Class handling the adding and removing of vehicles in the game.
	/// </summary>
	class GameVehicleDatabase : IGameObjectCollector {

		/// <summary>
		/// Singleton of the vehicle manager.
		/// </summary>
		public static GameVehicleDatabase Instance = null;

		/// <summary>
		/// List of AI vehicles and an ID to reference them by.
		/// </summary>
		public Dictionary<int, GameObject> vehiclesAI = new Dictionary<int, GameObject>();

		public GameVehicleDatabase() {
			Instance = this;
		}

		~GameVehicleDatabase() {
			Instance = null;
		}

		/// <summary>
		/// Handle collected objects destroy.
		/// </summary>
		public void DestroyObjects() {
			vehiclesAI.Clear();
		}

		/// <summary>
		/// Registers given gameObject as a vehicle if it's a vehicle.
		/// </summary>
		/// <param name="gameObject">The game object to check and eventually register.</param>
		public void CollectGameObject(GameObject gameObject) {

			if (gameObject.name == "Colliders") {
				return;
			}

			if (gameObject.transform.FindChild("CarColliderAI") != null) {
				if (vehiclesAI.ContainsValue(gameObject)) {
					Logger.Debug($"Duplicate AI vehicle prefab '{gameObject.name}' rejected");
				}
				else {
					vehiclesAI.Add(vehiclesAI.Count + 1, gameObject);
					Logger.Debug($"Registered AI vehicle prefab '{gameObject.name}' (AI Vehicle ID: {vehiclesAI.Count})");

					GameObject carCollider = gameObject.transform.FindChild("CarColliderAI").gameObject;
					carCollider.AddComponent<ObjectSyncComponent>().ObjectType = ObjectSyncManager.ObjectTypes.AIVehicle;
				}
			}
		}
	}
}
