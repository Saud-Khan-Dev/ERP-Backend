public class UserNotFoundException(string message) : NotFoundException(message);

public class RoleNotFoundException(string message) : NotFoundException(message);

public class PermissionNotFoundException(string message) : NotFoundException(message);

public class SessionNotFoundException(string message) : NotFoundException(message);
