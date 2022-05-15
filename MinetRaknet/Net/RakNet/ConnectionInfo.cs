#region LICENSE

// The contents of this file are subject to the Common Public Attribution
// License Version 1.0. (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// https://github.com/NiclasOlofsson/MiNET/blob/master/LICENSE.
// The License is based on the Mozilla Public License Version 1.1, but Sections 14
// and 15 have been added to cover use of software over a computer network and
// provide for limited attribution for the Original Developer. In addition, Exhibit A has
// been modified to be consistent with Exhibit B.
// 
// Software distributed under the License is distributed on an "AS IS" basis,
// WITHOUT WARRANTY OF ANY KIND, either express or implied. See the License for
// the specific language governing rights and limitations under the License.
// 
// The Original Code is MiNET.
// 
// The Original Developer is the Initial Developer.  The Initial Developer of
// the Original Code is Niclas Olofsson.
// 
// All portions of the code written by Niclas Olofsson are Copyright (c) 2014-2020 Niclas Olofsson.
// All Rights Reserved.

#endregion

using System.Collections.Concurrent;
using System.Net;

namespace MiNET.Net.RakNet
{
    public class ConnectionInfo
	{
		public ConcurrentDictionary<IPEndPoint, RakSession> RakSessions { get; set; }

		// Special property for use with ServiceKiller.
		// Will disable reliability handling after login.
		public bool IsEmulator { get; set; }
		public bool DisableAck { get; set; }

		public int NumberOfPlayers { get; set; }

		public long NumberOfAckReceive = 0;
		public long NumberOfNakReceive = 0;

		public int NumberOfDeniedConnectionRequestsPerSecond = 0;
		public long NumberOfAckSent = 0;
		public long NumberOfFails = 0;
		public long NumberOfResends = 0;
		public long NumberOfPacketsOutPerSecond = 0;
		public long NumberOfPacketsInPerSecond = 0;
		public long TotalPacketSizeOutPerSecond = 0;
		public long TotalPacketSizeInPerSecond = 0;

		public Timer ThroughPut { get; set; }
		public long Latency { get; set; }

		public int MaxNumberOfPlayers { get; set; }
		public int MaxNumberOfConcurrentConnects { get; set; }
		public int ConnectionsInConnectPhase = 0;

		public ConnectionInfo(ConcurrentDictionary<IPEndPoint, RakSession> rakSessions)
		{
			RakSessions = rakSessions;
		}

		internal void Stop()
		{
			ThroughPut?.Change(Timeout.Infinite, Timeout.Infinite);
			ThroughPut?.Dispose();
			ThroughPut = null;
		}
	}
}