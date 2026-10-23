namespace SmartParking.Domain.Constants;

public static class Roles
{
    public const string Admin = "ADMIN";
    public const string Manager = "MANAGER";
    public const string Staff = "STAFF";
    public const string User = "USER";

    public static readonly IReadOnlyList<string> All = [Admin, Manager, Staff, User];
}
