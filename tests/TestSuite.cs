using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using NSubstitute;

namespace Mutedac
{
    abstract class TestSuite<TContext> where TContext : IContext, new()
    {
        public static async Task<TContext> GetContext()
        {
            var context = new TContext();
            var fields = typeof(TContext)
                .GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(field => field.GetCustomAttributes(typeof(SubstituteAttribute), true).Length > 0);

            foreach (var field in fields)
            {
                var bindingFlags = BindingFlags.Static | BindingFlags.Public;
                var subForMethod = typeof(Substitute).GetMethod("For", 1, bindingFlags, null, new Type[] { typeof(object[]) }, null);
                var subForGeneric = subForMethod?.MakeGenericMethod(new Type[] { field.FieldType });
                var sub = subForGeneric?.Invoke(null, new object[] { Array.Empty<object>() });

                field.SetValue(context, sub);
            }

            await context.Setup();
            return context;
        }
    }
}
