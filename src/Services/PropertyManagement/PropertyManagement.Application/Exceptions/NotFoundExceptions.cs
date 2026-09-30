public class MasterDataNotFoundException(string message) : NotFoundException(message);

public class CodeSequenceNotFoundException(string message) : NotFoundException(message);

public class PropertyNotFoundException(string message) : NotFoundException(message);

public class MeasurementNotFoundException(string message) : NotFoundException(message);

public class RegularizationNotFoundException(string message) : NotFoundException(message);

public class OwnerNotFoundException(string message) : NotFoundException(message);

public class OwnershipNotFoundException(string message) : NotFoundException(message);

public class TransferNotFoundException(string message) : NotFoundException(message);

public class EncumbranceNotFoundException(string message) : NotFoundException(message);

public class DocumentNotFoundException(string message) : NotFoundException(message);

public class AttributeDefinitionNotFoundException(string message) : NotFoundException(message);

public class AllotmentNotFoundException(string message) : NotFoundException(message);

public class LeaseNotFoundException(string message) : NotFoundException(message);

public class RentalNotFoundException(string message) : NotFoundException(message);

public class ViolationNotFoundException(string message) : NotFoundException(message);

public class AuctionNotFoundException(string message) : NotFoundException(message);

public class OutsourcingNotFoundException(string message) : NotFoundException(message);

public class BoundaryNotFoundException(string message) : NotFoundException(message);

public class EncroachmentNotFoundException(string message) : NotFoundException(message);

public class LitigationNotFoundException(string message) : NotFoundException(message);

public class AppealNotFoundException(string message) : NotFoundException(message);

public class BuildingPlanNotFoundException(string message) : NotFoundException(message);

/// GDA Act s.2(a-i): the action is reserved for an officer authorized by the DG (fines s.28, complaints s.30).
public class AuthorizedOfficerRequiredException(string message) : Exception(message);
