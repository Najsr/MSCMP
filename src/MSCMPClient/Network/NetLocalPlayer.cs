﻿using HutongGames.PlayMaker;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace MSCMP.Network {

	/// <summary>
	/// Class handling local player state.
	/// </summary>
	class NetLocalPlayer : NetPlayer {

		/// <summary>
		/// How much time in seconds left until next synchronization packet will be sent.
		/// </summary>
		private float timeToUpdate = 0.0f;

		/// <summary>
		/// Synchronization interval in miliseconds.
		/// </summary>
		public const ulong SYNC_INTERVAL = 100;


		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="netManager">The network manager owning this player.</param>
		/// <param name="steamId">The steam id of the player.</param>
		public NetLocalPlayer(NetManager netManager, Steamworks.CSteamID steamId) : base(netManager, steamId) {
		}

#if !PUBLIC_RELEASE
		/// <summary>
		/// Update debug IMGUI for the player.
		/// </summary>
		public override void DrawDebugGUI() {
			GUI.Label(new Rect(300, 10, 300, 200), "Local player (time to update: " + timeToUpdate + ")");
		}
#endif

		/// <summary>
		/// The game object representing front doors of the yard.
		/// </summary>
		GameObject frontDoors = null;

		/// <summary>
		/// Delegate called when PlayMaker state action enters.
		/// </summary>
		delegate void OnEnterDelegate();

		/// <summary>
		/// Hooking wrapper for PlayMaker state action.
		/// </summary>
		private class StateActionProxy : FsmStateAction {

			FsmStateAction originalAction = null;
			OnEnterDelegate onEnter = null;
			public StateActionProxy(FsmStateAction originalAction, OnEnterDelegate onEnter) {
				this.originalAction = originalAction;
				this.onEnter = onEnter;
			}

			new public bool Active { get { return originalAction.Active; } set { originalAction.Active = value; } }
			new public bool Enabled { get { return originalAction.Enabled; } set { originalAction.Enabled = value; } }
			new public bool Entered { get { return originalAction.Entered; } set { originalAction.Entered = value; } }
			new public bool Finished { get { return originalAction.Finished; } set { originalAction.Finished = value; } }
			new public Fsm Fsm { get { return originalAction.Fsm; } set { originalAction.Fsm = value; } }
			new public bool IsOpen { get { return originalAction.IsOpen; } set { originalAction.IsOpen = value; } }
			new public string Name {
				get { return originalAction.Name; }
				set { originalAction.Name = value; }
			}
			new public GameObject Owner { get { return originalAction.Owner; } set { originalAction.Owner = value; } }
			new public FsmState State { get { return originalAction.State; } set { originalAction.State = value; } }

			public override void Awake() {
				originalAction.Awake();
			}
			public override void DoCollisionEnter(Collision collisionInfo) {
				originalAction.DoCollisionEnter(collisionInfo);
			}
			public override void DoCollisionExit(Collision collisionInfo) {
				originalAction.DoCollisionExit(collisionInfo);
			}
			public override void DoCollisionStay(Collision collisionInfo) {
				originalAction.DoCollisionStay(collisionInfo);
			}
			public override void DoControllerColliderHit(ControllerColliderHit collider) {
				originalAction.DoControllerColliderHit(collider);
			}
			public override void DoTriggerEnter(Collider other) {
				originalAction.DoTriggerEnter(other);
			}
			public override void DoTriggerExit(Collider other) {
				originalAction.DoTriggerExit(other);
			}
			public override void DoTriggerStay(Collider other) {
				originalAction.DoTriggerStay(other);
			}
			public override string ErrorCheck() {
				return originalAction.ErrorCheck();
			}
			public override bool Event(FsmEvent fsmEvent) {
				return originalAction.Event(fsmEvent);
			}
			public override void Init(FsmState state) {
				originalAction.Init(state);
			}
			public override void OnDrawGizmos() {
				originalAction.OnDrawGizmos();
			}
			public override void OnDrawGizmosSelected() {
				originalAction.OnDrawGizmosSelected();
			}
			public override void OnEnter() {
				onEnter();
				originalAction.OnEnter();
			}
			public override void OnExit() {
				originalAction.OnExit();
			}
			public override void OnFixedUpdate() {
				originalAction.OnFixedUpdate();
			}
			public override void OnGUI() {
				originalAction.OnGUI();
			}
			public override void OnLateUpdate() {
				originalAction.OnLateUpdate();
			}
			public override void OnUpdate() {
				originalAction.OnUpdate();
			}
			public override void Reset() {
				originalAction.Reset();
			}
		}

		/// <summary>
		/// Adds new event into the given PlayMaker FSM.
		/// </summary>
		/// <param name="fsm">The PlayMaker FSM to add the event to.</param>
		/// <param name="name">The name of the event to add.</param>
		/// <returns>Added event object.</returns>
		private FsmEvent AddNewEvent(PlayMakerFSM fsm, string name) {
			FsmEvent[] oldEvents = fsm.Fsm.Events;
			List<FsmEvent> temp = new List<FsmEvent>();
			foreach (FsmEvent e in oldEvents) {
				temp.Add(e);
			}

			FsmEvent ev = new FsmEvent(name);
			temp.Add(ev);
			fsm.Fsm.Events = temp.ToArray();
			return ev;
		}

		/// <summary>
		/// Add new global transition from the given event to the state name to the given PlayMaker FSM.
		/// </summary>
		/// <param name="fsm">The PlayMaker FSM to add global transition for.</param>
		/// <param name="ev">The event triggering the transition.</param>
		/// <param name="stateName">The state this transition activates.</param>
		private void AddNewGlobalTransition(PlayMakerFSM fsm, FsmEvent ev, string stateName) {
			FsmTransition[] oldTransitions = fsm.FsmGlobalTransitions;
			List<FsmTransition> temp = new List<FsmTransition>();
			foreach (FsmTransition t in oldTransitions) {
				temp.Add(t);
			}
			FsmTransition transition = new FsmTransition();
			transition.FsmEvent = ev;
			transition.ToState = stateName;
			temp.Add(transition);

			fsm.Fsm.GlobalTransitions = temp.ToArray();
		}

		/// <summary>
		/// Open the given doors.
		/// </summary>
		/// <param name="doorsName">The name of the doors object to open.</param>
		/// <param name="open">Should the doors be opened?</param>
		public void OpenDoors(string doorsName, bool open) {
			if (frontDoors != null) {
				PlayMakerFSM fsm = Utils.GetPlaymakerScriptByName(frontDoors, "Use");
				if (open) {
					fsm.SendEvent("MPOPEN");
				}
				else {
					fsm.SendEvent("MPCLOSE");
				}
			}
		}

		/// <summary>
		/// Update network doors state.
		/// </summary>
		public void UpdateDoors() {
			if (frontDoors == null) {
				frontDoors = GameObject.Find("DoorFront");

				if (frontDoors != null) {
					PlayMakerFSM fsm = Utils.GetPlaymakerScriptByName(frontDoors, "Use");

					FsmEvent mpOpenEvent = AddNewEvent(fsm, "MPOPEN");
					FsmEvent mpCloseEvent = AddNewEvent(fsm, "MPCLOSE");

					AddNewGlobalTransition(fsm, mpOpenEvent, "Open door");
					AddNewGlobalTransition(fsm, mpCloseEvent, "Close door");

					foreach (FsmState s in fsm.FsmStates) {

						if (s.Name == "Open door") {
							FsmStateAction[] actions = s.Actions;
							StateActionProxy action = new StateActionProxy(actions[0], () => {
								if (s.Fsm.LastTransition.EventName == "MPOPEN") {
									return;
								}
								MPController.logFile.WriteLine("Doors opened");

								Messages.OpenDoorsMessage message = new Messages.OpenDoorsMessage();
								message.doorName = "DoorFront";
								message.open = true;
								netManager.BroadcastMessage(message, Steamworks.EP2PSend.k_EP2PSendReliable);
							});
							actions[0] = action;
						}

						if (s.Name == "Close door") {

							FsmStateAction[] actions = s.Actions;
							StateActionProxy action = new StateActionProxy(actions[0], () => {
								if (s.Fsm.LastTransition.EventName == "MPCLOSE") {
									return;
								}
								MPController.logFile.WriteLine("Doors closed");
								Messages.OpenDoorsMessage message = new Messages.OpenDoorsMessage();
								message.doorName = "DoorFront";
								message.open = false;
								netManager.BroadcastMessage(message, Steamworks.EP2PSend.k_EP2PSendReliable);
							});
							actions[0] = action;
						}
					}
				}
			}
		}

		/// <summary>
		/// Update state of the local player.
		/// </summary>
		public override void Update() {
			UpdateDoors();

			// Synchronization sending.

			timeToUpdate -= Time.deltaTime;
			if (timeToUpdate <= 0.0f) {

				GameObject obj = GameObject.Find("PLAYER");
				if (!obj) return;
				if (!obj.transform) return;
				Vector3 position = obj.transform.position;
				Quaternion rotation = obj.transform.rotation;

				Messages.PlayerSyncMessage message = new Messages.PlayerSyncMessage();
				message.position.x = position.x;
				message.position.y = position.y;
				message.position.z = position.z;

				message.rotation.w = rotation.w;
				message.rotation.x = rotation.x;
				message.rotation.y = rotation.y;
				message.rotation.z = rotation.z;
				netManager.BroadcastMessage(message, Steamworks.EP2PSend.k_EP2PSendUnreliable);

				timeToUpdate = (float)SYNC_INTERVAL / 1000.0f;
			}
		}
	}
}