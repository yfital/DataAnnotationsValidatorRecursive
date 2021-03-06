﻿﻿using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace DataAnnotationsValidator
{
    public class DataAnnotationsValidator : IDataAnnotationsValidator
    {
        public bool TryValidateObject(object obj, ICollection<ValidationResult> results,
            IDictionary<object, object> validationContextItems = null)
        {
            return Validator.TryValidateObject(obj, new ValidationContext(obj, null, validationContextItems), results, true);
        }

        public bool TryValidateObjectRecursive<T>(T obj, List<ValidationResult> results,
            IDictionary<object, object> validationContextItems = null)
        {
            return TryValidateObjectRecursive(obj, results, new HashSet<object>(), validationContextItems);
        }

        private bool TryValidateObjectRecursive<T>(T obj, List<ValidationResult> results, ISet<object> validatedObjects,
            IDictionary<object, object> validationContextItems = null)
        {
            //short-circuit to avoid infinite loops on cyclical object graphs
            if (validatedObjects.Contains(obj))
            {
                return true;
            }

            validatedObjects.Add(obj);
            var result = TryValidateObject(obj, results, validationContextItems);

            var properties = obj.GetType().GetProperties().Where(prop => prop.CanRead
                                                                         && !prop.GetCustomAttributes(typeof(SkipRecursiveValidation),
                                                                             false).Any()
                                                                         && prop.GetIndexParameters().Length == 0).ToList();

            foreach (var property in properties)
            {
                if (property.PropertyType == typeof(string) || property.PropertyType.IsValueType)
                    continue;

                var value = obj.GetPropertyValue(property.Name);

                switch (value)
                {
                    case null:
                        continue;
                    case IEnumerable enumerable:
                    {
                        foreach (var enumObj in enumerable)
                        {
                            if (enumObj == null)
                                continue;
                            var nestedResults = new List<ValidationResult>();
                            if (!TryValidateObjectRecursive(enumObj, nestedResults, validatedObjects, validationContextItems))
                            {
                                result = false;
                                foreach (var validationResult in nestedResults)
                                {
                                    var property1 = property;
                                    results.Add(new ValidationResult(validationResult.ErrorMessage,
                                        validationResult.MemberNames.Select(x => property1.Name + '.' + x)));
                                }
                            }
                        }

                        break;
                    }
                    default:
                    {
                        var nestedResults = new List<ValidationResult>();
                        if (!TryValidateObjectRecursive(value, nestedResults, validatedObjects, validationContextItems))
                        {
                            result = false;
                            foreach (var validationResult in nestedResults)
                            {
                                var property1 = property;
                                results.Add(new ValidationResult(validationResult.ErrorMessage,
                                    validationResult.MemberNames.Select(x => property1.Name + '.' + x)));
                            }
                        }
                        break;
                    }
                }
            }

            return result;
        }
    }
}