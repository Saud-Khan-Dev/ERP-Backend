public sealed record AttendanceRecordId : ITypedId<AttendanceRecordId>
{
  public Guid Value { get; }

  private AttendanceRecordId(Guid value) => Value = value;

  public static AttendanceRecordId Of(Guid value)
  {
    if (value == Guid.Empty)
      throw new DomainException("Attendance record id cannot be empty.");

    return new AttendanceRecordId(value);
  }

  public static AttendanceRecordId New() => new(Guid.NewGuid());

  public override string ToString() => Value.ToString();
}
