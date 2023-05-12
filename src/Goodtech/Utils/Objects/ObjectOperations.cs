using System.Linq;

namespace Goodtech.Utils.Objects
{
    /// <summary>
    /// This class is used for different object operations, such as comparing objects. Similar to .Equals() for instance.
    /// </summary>
    public static class ObjectOperations
    {
        /// <summary>
        /// Compares two objects properties against each other and checks if the objects have the same properties.
        /// </summary>
        /// <param name="obj1">The first object to compare.</param>
        /// <param name="obj2">The second object to compare.</param>
        /// <returns>True if the objects have the same properties. False if the objects have different properties.</returns>
        public static bool CompareProperties(object obj1, object obj2)
        {
            return obj1.GetType().GetProperties().All(prop => obj2.GetType().GetProperty(prop.Name) != null);
        }

        /// <summary>
        /// Compares the values of two objects against each other. Similar to .Equals().
        /// Calls <see cref="CompareProperties"/> first, to check that the objects have the same properties.
        /// </summary>
        /// <param name="obj1">The first object to compare.</param>
        /// <param name="obj2">The second object to compare.</param>
        /// <returns>True if the values are equal and the objects are similar. False if the values are not equal, or if the objects aren't similar.</returns>
        public static bool ComparePropertiesValues(object obj1, object obj2)
        {
            //Return false if objects do not have the same structure
            if (!CompareProperties(obj1, obj2)) return false;
            //Loop through properties and check values against each other.
            foreach (var propSource in obj2.GetType().GetProperties())
            {
                var propTarget = obj1.GetType().GetProperty(propSource.Name);
                if (propSource.GetValue(obj1) != propTarget!.GetValue(obj2)) return false;
            }
            return true;
        }
    }
}