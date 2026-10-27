using Microsoft.AspNetCore.Mvc;

namespace DocQuery.Api.Controllers;

public abstract class BaseController : ControllerBase
{
    // Reads the logged-in user's id from the JWT ("userId" claim).
    protected int CurrentUserId => int.Parse(User.FindFirst("userId")!.Value);
}
