using Entities.Abstract;



namespace Entities.Concrete.Entities

{

    public class SocialProfileMute : IEntity

    {

        public Guid Id { get; set; }

        public Guid MutedByProfileId { get; set; }

        public SocialProfile MutedByProfile { get; set; } = null!;

        public Guid MutedProfileId { get; set; }

        public SocialProfile MutedProfile { get; set; } = null!;

        public DateTime CreatedAt { get; set; }

    }

}

