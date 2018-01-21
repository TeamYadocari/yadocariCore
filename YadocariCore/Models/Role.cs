#region Copyright
// /*
//  * Role.cs
//  *
//  * Copyright (c) 2018 TeamYadocari
//  *
//  * You can redistribute it and/or modify it under either the terms of
//  * the AGPLv3 or YADOCARI binary code license. See the file COPYING
//  * included in the YADOCARI package for more in detail.
//  *
//  */
#endregion
using System;

namespace YadocariCore.Models
{
    [Flags]
    public enum Role
    {
        Administrator = 1,
        Upload = 2,
        Access = 4
    }
}