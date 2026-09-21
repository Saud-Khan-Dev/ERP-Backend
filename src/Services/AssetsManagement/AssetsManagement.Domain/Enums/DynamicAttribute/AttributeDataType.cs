public enum AttributeDataType
{
  Text,
  LongText,
  Integer,
  Decimal,
  Boolean,
  Date,
  DateTime,
  Select,
  MultiSelect,
  Json,
  Reference,   // FK-ish pointer to another entity (employee, supplier, asset)
  File         // points at asset_attachment.id
}
