/// A record that cannot be deleted because other records use it (409, with the reason in a plain sentence).
public class InUseException(string message) : Exception(message);
