using Entities.Concrete.Enums;



namespace Entities.Concrete.Dto

{

    public class SocialProfileUpdateDto

    {

        public string? Username { get; set; }

        public string? Bio { get; set; }

        public string? ExternalUrl { get; set; }

        public bool? IsPrivate { get; set; }

        public SocialDmPolicy? DmPolicy { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

    }

}

