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
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using Mooege.Core.Common.Storage;
using Mooege.Core.Common.Toons;
using Mooege.Core.MooNet.Helpers;
using Mooege.Core.MooNet.Objects;
using Mooege.Net.MooNet;

namespace Mooege.Core.MooNet.Accounts
{
    public class Account : PersistentRPCObject
    {
        public bnet.protocol.EntityId BnetAccountID { get; private set; }
        public bnet.protocol.EntityId BnetGameAccountID { get; private set; }
        public D3.Account.BannerConfiguration BannerConfiguration { get; private set; }
        public string Email { get; private set; }

        public MooNetClient LoggedInClient { get; set; }

        public D3.Account.Digest Digest
        {
            get
            {
                var builder = D3.Account.Digest.CreateBuilder().SetVersion(99)
                    .SetBannerConfiguration(this.BannerConfiguration)
                    .SetFlags(0);

                builder.SetLastPlayedHeroId(
                    (Toons.Count > 0)
                    ? Toons.First().Value.D3EntityID
                    : D3.OnlineService.EntityId.CreateBuilder().SetIdHigh(0).SetIdLow(0)
                    .Build());
                return builder.Build();
            }
        }

        public Dictionary<ulong, Toon> Toons
        {
            get { return ToonManager.GetToonsForAccount(this); }
        }

        public Account(ulong persistantId, string email) // Account with given persistent ID
            : base(persistantId)
        {
            this.SetFields(email);
        }

        public Account(string email) // Account with **newly generated** persistent ID
            : base()
        {
            this.SetFields(email);
        }

        private static ulong? _persistantIdCounter = null;
        protected override ulong GenerateNewPersistentId()
        {
            if (_persistantIdCounter == null)
                _persistantIdCounter = AccountManager.GetNextAvailablePersistantId();

            return (ulong)++_persistantIdCounter;
        }

        private void SetFields(string email)
        {
            this.Email = email;
            this.BnetAccountID = bnet.protocol.EntityId.CreateBuilder().SetHigh((ulong)EntityIdHelper.HighIdType.AccountId).SetLow(this.PersistentID).Build();
            this.BnetGameAccountID = bnet.protocol.EntityId.CreateBuilder().SetHigh((ulong)EntityIdHelper.HighIdType.GameAccountId).SetLow(this.PersistentID).Build();
            this.BannerConfiguration = D3.Account.BannerConfiguration.CreateBuilder()
                .SetBackgroundColorIndex(20)
                .SetBannerIndex(8)
                .SetPattern(4)
                .SetPatternColorIndex(11)
                .SetPlacementIndex(11)
                .SetSigilAccent(4)
                .SetSigilMain(3)
                .SetSigilColorIndex(7)
                .SetUseSigilVariant(true)
                .Build();
        }

        public bnet.protocol.presence.Field QueryField(bnet.protocol.presence.FieldKey queryKey)
        {
            var field = bnet.protocol.presence.Field.CreateBuilder().SetKey(queryKey);

            switch ((FieldKeyHelper.Program)queryKey.Program)
            {
                case FieldKeyHelper.Program.D3:
                    if (queryKey.Group == 1 && queryKey.Field == 1) // Account's selected toon.
                    {
                        if(this.LoggedInClient!=null) // check if the account is online actually.
                        field.SetValue(bnet.protocol.attribute.Variant.CreateBuilder().SetMessageValue(this.LoggedInClient.CurrentToon.D3EntityID.ToByteString()).Build());
                    }
                    else
                    {
                        Logger.Warn("Unknown query-key: {0}, {1}, {2}", queryKey.Program, queryKey.Group, queryKey.Field);
                    }
                    break;
                case FieldKeyHelper.Program.BNet:
                    Logger.Warn("Unknown query-key: {0}, {1}, {2}", queryKey.Program, queryKey.Group, queryKey.Field);
                    break;
            }


            return field.HasValue ? field.Build() : null;
        }

        protected override void NotifySubscriptionAdded(MooNetClient client)
        {
            var operations = new List<bnet.protocol.presence.FieldOperation>();

            // RealID name field - NOTE: Probably won't ever use this for its actual purpose, but showing the email in final might not be a good idea
            var fieldKey1 = FieldKeyHelper.Create(FieldKeyHelper.Program.BNet,1, 1, 0);
            var field1 = bnet.protocol.presence.Field.CreateBuilder().SetKey(fieldKey1).SetValue(bnet.protocol.attribute.Variant.CreateBuilder().SetStringValue(this.Email).Build()).Build();
            operations.Add(bnet.protocol.presence.FieldOperation.CreateBuilder().SetField(field1).Build());

            // Hardcoded boolean - always true
            var fieldKey2 = FieldKeyHelper.Create(FieldKeyHelper.Program.BNet, 1, 2, 0);
            var field2 = bnet.protocol.presence.Field.CreateBuilder().SetKey(fieldKey2).SetValue(bnet.protocol.attribute.Variant.CreateBuilder().SetBoolValue(true).Build()).Build();
            operations.Add(bnet.protocol.presence.FieldOperation.CreateBuilder().SetField(field2).Build());

            // Selected toon
            if (this.LoggedInClient != null && this.LoggedInClient.CurrentToon!=null)
            {
                var fieldKey3 = FieldKeyHelper.Create(FieldKeyHelper.Program.D3, 1, 1, 0);
                var field3 = bnet.protocol.presence.Field.CreateBuilder().SetKey(fieldKey3).SetValue(bnet.protocol.attribute.Variant.CreateBuilder().SetMessageValue(this.LoggedInClient.CurrentToon.D3EntityID.ToByteString()).Build()).Build();
                operations.Add(bnet.protocol.presence.FieldOperation.CreateBuilder().SetField(field3).Build());
            }

            // toon list
            foreach(var pair in this.Toons)
            {
                var fieldKey = FieldKeyHelper.Create(FieldKeyHelper.Program.BNet, 1, 4, 0);
                var field = bnet.protocol.presence.Field.CreateBuilder().SetKey(fieldKey).SetValue(bnet.protocol.attribute.Variant.CreateBuilder().SetMessageValue(pair.Value.BnetEntityID.ToByteString()).Build()).Build();
                operations.Add(bnet.protocol.presence.FieldOperation.CreateBuilder().SetField(field).Build());
            }

            // Create a presence.ChannelState
            var state = bnet.protocol.presence.ChannelState.CreateBuilder().SetEntityId(this.BnetAccountID).AddRangeFieldOperation(operations).Build();

            // Embed in channel.ChannelState
            var channelState = bnet.protocol.channel.ChannelState.CreateBuilder().SetExtension(bnet.protocol.presence.ChannelState.Presence, state);

            // Put in addnotification message
            var builder = bnet.protocol.channel.AddNotification.CreateBuilder().SetChannelState(channelState);

            // Make the rpc call
            client.CallMethod(bnet.protocol.channel.ChannelSubscriber.Descriptor.FindMethodByName("NotifyAdd"), builder.Build(), this.DynamicId);
        }

        public void SaveToDB()
        {
            try
            {
                var query =
                    string.Format(
                        "INSERT INTO accounts (id, email) VALUES({0},'{1}')",
                        this.PersistentID, this.Email);

                    var cmd = new SQLiteCommand(query, DBManager.Connection);
                    cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                Logger.ErrorException(e, "SaveToDB()");
            }
        }
    }
}
