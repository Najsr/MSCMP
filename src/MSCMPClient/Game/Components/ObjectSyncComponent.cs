using UnityEngine;
using MSCMP.Network;
using MSCMP.Game.Objects;

namespace MSCMP.Game.Components {
	/// <summary>
	/// Attached to objects that require position/rotation sync.
	/// Sync is provided based on distance from the player and paramters inside an ISyncedObject.
	/// </summary>
	class ObjectSyncComponent : MonoBehaviour {
		/// <summary>
		/// If sync is enabled.
		/// </summary>
		public bool SyncEnabled = false;

		/// <summary>
		/// Sync owner of the object.
		/// </summary>
		public NetPlayer Owner = null;

		/// <summary>
		/// Object ID.
		/// </summary>
		public int ObjectID = ObjectSyncManager.AUTOMATIC_ID;

		/// <summary>
		/// Object type.
		/// </summary>
		public ObjectSyncManager.ObjectTypes ObjectType;

		/// <summary>
		/// If sync of an object should be sent constantly.
		/// </summary>
		bool sendConstantSync = false;

		/// <summary>
		/// The object sub-type that is being synced.
		/// </summary>
		ISyncedObject syncedObject;

		/// <summary>
		/// True if the object is setup and ready to sync.
		/// </summary>
		public bool IsSetup = false;

		/// <summary>
		/// GameObject this component is attached to. Used as a reference for when object is disabled.
		/// </summary>
		GameObject thisObject;


		/// <summary>
		/// Constructor.
		/// </summary>
		public ObjectSyncComponent() {
			thisObject = this.gameObject;
		}

		/// <summary>
		/// Setup object.
		/// </summary>
		/// <param name="type">Object type.</param>
		/// <param name="objectID">Object ID to assign.</param>
		/// <returns>Assigned Object ID</returns>
		public int Setup(ObjectSyncManager.ObjectTypes type, int objectID) {
			if (!NetWorld.Instance.playerIsLoading) {
				if (!NetManager.Instance.IsHost && objectID == ObjectSyncManager.AUTOMATIC_ID) {
					Logger.Debug("Ignoring spawned object as client is not host!");
					GameObject.Destroy(gameObject);
					return -1;
				}
			}
			IsSetup = false;
			SyncEnabled = false;
			Owner = null;
			ObjectType = type;
			ObjectID = objectID;

			// Assign object's ID.
			ObjectID = ObjectSyncManager.Instance.AddNewObject(this, ObjectID);

			if (!NetWorld.Instance.playerIsLoading && !IsSetup) {
				CreateObjectSubtype();
			}

			return ObjectID;
		}

		/// <summary>
		/// Called on start.
		/// </summary>
		void Start() {
			if (NetWorld.Instance.playerIsLoading && !IsSetup) {
				CreateObjectSubtype();
			}
		}

		/// <summary>
		/// Creates the object's subtype.
		/// </summary>
		void CreateObjectSubtype() {
			// Set object type.
			switch (ObjectType) {
				// Pickupable.
				case ObjectSyncManager.ObjectTypes.Pickupable:
					syncedObject = new Pickupable(this.gameObject);
					break;
				// AI Vehicle.
				case ObjectSyncManager.ObjectTypes.AIVehicle:
					syncedObject = new AIVehicle(this.gameObject, this);
					break;
				// Boat.
				case ObjectSyncManager.ObjectTypes.Boat:
					syncedObject = new Boat(this.gameObject);
					break;
				// Garage door.
				case ObjectSyncManager.ObjectTypes.GarageDoor:
					syncedObject = new GarageDoor(this.gameObject);
					break;
				// Player vehicle.
				case ObjectSyncManager.ObjectTypes.PlayerVehicle:
					syncedObject = new PlayerVehicle(this.gameObject, this);
					break;
			}
			IsSetup = true;
		}

		/// <summary>
		/// Called once per frame.
		/// </summary>
		void Update() {
			if (!IsSetup && !SyncEnabled) {
				return;
			}

			// Updates object's position continuously, or, if the CanSync criteria is met.
			if (syncedObject.CanSync() || sendConstantSync) {
				SendObjectSync(ObjectSyncManager.SyncTypes.GenericSync, true, false);
			}

			// Periodically update the object's position if periodic sync is enabled.
			if (syncedObject.PeriodicSyncEnabled() && ObjectSyncManager.Instance.ShouldPeriodicSync(Owner, SyncEnabled)) {
				SendObjectSync(ObjectSyncManager.SyncTypes.PeriodicSync, true, false);
			}
		}

		/// <summary>
		/// Sends a sync update of the object.
		/// </summary>
		public void SendObjectSync(ObjectSyncManager.SyncTypes type, bool sendVariables, bool syncWasRequested) {
			if (sendVariables) {
				NetLocalPlayer.Instance.SendObjectSync(ObjectID, syncedObject.ObjectTransform().position, syncedObject.ObjectTransform().rotation, type, syncedObject.ReturnSyncedVariables(true));
			}
			else {
				NetLocalPlayer.Instance.SendObjectSync(ObjectID, syncedObject.ObjectTransform().position, syncedObject.ObjectTransform().rotation, type, null);
			}
		}

		/// <summary>
		/// Request a sync update from the host.
		/// </summary>
		public void RequestObjectSync() {
			NetLocalPlayer.Instance.RequestObjectSync(ObjectID);
		}

		/// <summary>
		/// Called when object sync request is accepted by the remote client.
		/// </summary>
		public void SyncRequestAccepted() {
			Owner = Steamworks.SteamUser.GetSteamID().m_SteamID;
			Logger.Log("Sync request accepted, object: " + gameObject.name);
			SyncEnabled = true;
		}

		/// <summary>
		/// Called when the player enter sync range of the object.
		/// </summary>
		public void SendEnterSync() {
			if (Owner == ObjectSyncManager.NO_OWNER && syncedObject.ShouldTakeOwnership()) {
				SendObjectSync(ObjectSyncManager.SyncTypes.SetOwner, true, false);
			}
		}

		/// <summary>
		/// Called when the player exits sync range of the object.
		/// </summary>
		public void SendExitSync() {
			if (Owner == ObjectSyncManager.Instance.steamID.m_SteamID) {
				Owner = ObjectSyncManager.NO_OWNER;
				SyncEnabled = false;
				SendObjectSync(ObjectSyncManager.SyncTypes.RemoveOwner, false, false);
			}
		}

		/// <summary>
		/// Take sync control of the object by force.
		/// </summary>
		public void TakeSyncControl() {
			if (Owner != Steamworks.SteamUser.GetSteamID().m_SteamID) {
				SendObjectSync(ObjectSyncManager.SyncTypes.ForceSetOwner, true, false);
			}
		}

		/// <summary>
		/// Called when sync owner is set to the remote client.
		/// </summary>
		public void OwnerSetToRemote(ulong newOwner) {
			Owner = newOwner;
			syncedObject?.OwnerSetToRemote();
		}

		/// <summary>
		/// Called when owner is removed.
		/// </summary>
		public void OwnerRemoved() {
			Owner = null;
			syncedObject?.OwnerRemoved();
		}

		/// <summary>
		/// Called when sync control of an object has been taken from local player.
		/// </summary>
		public void SyncTakenByForce() {
			syncedObject?.SyncTakenByForce();
		}

		/// <summary>
		/// Set object to send position and rotation sync constantly.
		/// </summary>
		/// <param name="newValue">If object should be constantly synced.</param>
		public void SendConstantSync(bool newValue) {
			sendConstantSync = newValue;
			syncedObject?.ConstantSyncChanged(newValue);
		}

		/// <summary>
		/// Handles synced variables sent from remote client.
		/// </summary>
		/// <param name="syncedVariables">Synced variables</param>
		public void HandleSyncedVariables(float[] syncedVariables) {
			syncedObject?.HandleSyncedVariables(syncedVariables);
		}

		/// <summary>
		/// Check if object owner is local client.
		/// </summary>
		/// <returns>True is object owner is self.</returns>
		public bool IsOwnerSelf() {
			if (Owner != Steamworks.SteamUser.GetSteamID().m_SteamID) {
				return false;
			}
			else {
				return true;
			}
		}

		/// <summary>
		/// Set object's postion and rotationn.
		/// </summary>
		/// <param name="pos"></param>
		/// <param name="rot"></param>
		public void SetPositionAndRotation(Vector3 pos, Quaternion rot) {
			if (syncedObject == null) {
				// Can be caused by moving an object whilst the remote client is still loading.
				// Object should become synced after the client has finished loading anyway.
				Logger.Debug($"Tried to set position of object '{gameObject.name}' but object isn't setup. (This is usually fine)");
				return;
			}
			if (syncedObject != null) {
				syncedObject.ObjectTransform().position = pos;
				syncedObject.ObjectTransform().rotation = rot;
			}
		}

		/// <summary>
		/// Return the GameObject of this component.
		/// </summary>
		/// <returns>GameObject.</returns>
		public GameObject GetGameObject() {
			return thisObject;
		}

		/// <summary>
		/// Return the object subtype componennt.
		/// </summary>
		/// <returns>Synced object component.</returns>
		public ISyncedObject GetObjectSubtype() {
			return syncedObject;
		}
	}
}
