using System;
using Microsoft.AspNetCore.Identity;

namespace NZFurs.Auth.Models
{
    // Add profile data for application users by adding properties to the ApplicationUser class
    public class ApplicationUser : IdentityUser
    {
        public bool IsAdmin { get; set; }

        // Fursona related fields
        public string FursonaName { get; set; }
        public string FursonaSpecies { get; set; }

        // TODO: Alternate fursonas

        // Irl identity fields
        public string IrlFullName { get; set; }
        public string IrlShortName { get; set; }

        // Birthday fields
        public DateTime? DateOfBirth { get; set; }
        public DateOfBirthPublicFlags DateOfBirthPublicFlags { get; set; }
        // TODO: Birthday, age, is18+
    }
}