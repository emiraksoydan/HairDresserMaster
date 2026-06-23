namespace Entities.Concrete.Dto

{

    public class SocialMutualFollowersDto

    {

        public int Count { get; set; }

        public List<SocialFollowListItemDto> Preview { get; set; } = new();

    }

}

