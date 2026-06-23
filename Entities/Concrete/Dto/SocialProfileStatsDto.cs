namespace Entities.Concrete.Dto
{
    public class SocialProfileStatsDto
    {
        public int PostCount { get; set; }
        public int FollowerCount { get; set; }
        public int FollowingCount { get; set; }
        public bool IsFollowing { get; set; }
    }
}
