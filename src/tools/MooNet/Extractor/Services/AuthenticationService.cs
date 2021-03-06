﻿/*
 * Copyright (C) 2011 mooege project
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
 */

using System;
using Google.ProtocolBuffers;
using bnet.protocol;
using bnet.protocol.authentication;

namespace Extractor.Services
{
    [Service(serviceID: 0x1, serviceName: "bnet.protocol.authentication.AuthenticationServer")]
    public class AuthenticationService:AuthenticationServer
    {
        public override void Logon(IRpcController controller, LogonRequest request, Action<LogonResponse> done)
        {
            ProtoOutputBuffer.Write(request.GetType(), request.ToString());
        }

        public override void ModuleMessage(IRpcController controller, ModuleMessageRequest request, Action<NoData> done)
        {
            ProtoOutputBuffer.Write(request.GetType(), request.ToString());
        }
    }
}
