﻿namespace MSCMPMessages.Messages {
	[NetMessageDesc(MessageIds.VehicleState)]
	class VehicleStateMessage {
		byte vehicleId;
		int state;
	}
}
