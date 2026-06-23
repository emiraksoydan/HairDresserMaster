using Business.Abstract;

using Entities.Concrete.Dto;

using Microsoft.AspNetCore.Mvc;



namespace Api.Controllers

{

    [Route("api/social/appointment-share")]

    public class SocialAppointmentShareController : BaseApiController

    {

        private readonly ISocialAppointmentShareService _socialAppointmentShareService;



        public SocialAppointmentShareController(ISocialAppointmentShareService socialAppointmentShareService)

        {

            _socialAppointmentShareService = socialAppointmentShareService;

        }



        [HttpPost("status")]

        public async Task<IActionResult> GetStatus([FromBody] AppointmentShareStatusRequestDto request)

        {

            return await HandleUserDataOperation(userId =>

                _socialAppointmentShareService.GetSharedAppointmentIdsAsync(

                    userId,

                    request?.AppointmentIds ?? new List<Guid>()));

        }

    }

}

