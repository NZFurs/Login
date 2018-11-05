using System;

namespace NZFurs.Auth.Models
{
    [Flags]
    public enum DateOfBirthPublicFlags
    {
        BirthdayPublic = 0x01,
        AgePublic = 0x02,
    }
}