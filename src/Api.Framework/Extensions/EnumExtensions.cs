using System.ComponentModel;

namespace Api.Framework.Extensions;

public static class EnumExtensions
{
    public static string Description(this Enum element, params object[] extraObjets)
    {
        var type = element.GetType();
        var memberInfo = type.GetMember(element.ToString());
        var attributes = memberInfo[0].GetCustomAttributes(typeof(DescriptionAttribute), false);
        var description = attributes.Length > 0
            ? ((DescriptionAttribute)attributes[0]).Description
            : element.ToString();
        return string.Format(description, extraObjets);
    }

    //checks to see if an enumerated value contains a type
    public static bool Has<T>(this Enum type, T value)
    {
        try
        {
            return (((int)(object)type &
                     (int)(object)value!) == (int)(object)value);
        }
        catch
        {
            return false;
        }
    }

    //checks if the value is only the provided type
    public static bool Is<T>(this Enum type, T value)
    {
        try
        {
            return (int)(object)type == (int)(object)value!;
        }
        catch
        {
            return false;
        }
    }

    //appends a value
    public static T Add<T>(this Enum type, T value)
    {
        try
        {
            return (T)(object)(((int)(object)type | (int)(object)value!));
        }
        catch (Exception ex)
        {
            throw new ArgumentException(
                $"Could not append value from enumerated type '{typeof(T).Name}'.", ex);
        }
    }

    //completely removes the value
    public static T Remove<T>(this Enum type, T value)
    {
        try
        {
            return (T)(object)(((int)(object)type & ~(int)(object)value!));
        }
        catch (Exception ex)
        {
            throw new ArgumentException(
                $"Could not remove value from enumerated type '{typeof(T).Name}'.", ex);
        }
    }
}
