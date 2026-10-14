public class TaxFileNotFoundException(string message) : NotFoundException(message);
public class TaxCircleNotFoundException(string message) : NotFoundException(message);
public class TaxHeadNotFoundException(string message) : NotFoundException(message);
public class RateSetNotFoundException(string message) : NotFoundException(message);
public class SpecialPolicyNotFoundException(string message) : NotFoundException(message);
public class PenaltyRuleNotFoundException(string message) : NotFoundException(message);
public class DemandRunNotFoundException(string message) : NotFoundException(message);
public class DemandNotFoundException(string message) : NotFoundException(message);
public class NoticeNotFoundException(string message) : NotFoundException(message);
public class PaymentNotFoundException(string message) : NotFoundException(message);
public class FeeNotFoundException(string message) : NotFoundException(message);
public class PartyNotFoundException(string message) : NotFoundException(message);
public class ClearanceNotFoundException(string message) : NotFoundException(message);
public class TemplateNotFoundException(string message) : NotFoundException(message);
public class PropertyNotFoundException(string message) : NotFoundException(message);
public class StoredFileNotFoundException(string message) : NotFoundException(message);

/// Property or HRM did not answer, or answered with an error Taxation cannot act on (503 to the caller).
public class ExternalServiceException(string message, Exception? inner = null) : Exception(message, inner);
