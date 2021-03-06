﻿namespace PluralKit.Core
{
    public class SystemGuildSettings
    {
        public ulong Guild { get; }
        public SystemId System { get; }
        public bool ProxyEnabled { get; } = true;

        public AutoproxyMode AutoproxyMode { get; } = AutoproxyMode.Off;
        public MemberId? AutoproxyMember { get; }
    }
}