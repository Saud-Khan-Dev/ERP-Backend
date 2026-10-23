/// The operation half of a MODULE.ACTION permission code. A closed set, so codes cannot drift
/// between EDIT and UPDATE. Mirrors PermissionCatalog.Actions.
public enum PermissionAction
{
  View,
  Create,
  Edit,
  Delete,
  Approve,
  Post,
  Assign,
  Revoke,
  Export,
  Run,
  Reset,
  Disable,
  Override,
  Reverse,
  Waive,
  Issue,
  Adjust,
  Pay
}
