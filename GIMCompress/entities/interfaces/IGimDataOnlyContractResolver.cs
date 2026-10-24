using GIMcompress;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GIMCompress.entities.interfaces
{
    public class IGimDataOnlyContractResolver : DefaultContractResolver
    {
        private static Type GimDataClassType = typeof(GimData);
        private static Type GimElementClassType = typeof(GimElement);
        private static Type GimLocalClassType = typeof(GimLocal);
        private static Type GimObjectClassType = typeof(GimObject);

        private static Type GimDataInterfaceType = typeof(IReadOnlyGimData);
        private static Type GimElementInterfaceType = typeof(IReadOnlyGimElement);
        private static Type GimLocalInterfaceType = typeof(IReadOnlyGimLocal);
        private static Type GimObjectInterfaceType = typeof(IReadOnlyGimObject);
        public IGimDataOnlyContractResolver()
        {
        }

        public static Type GetGimInterface(Type type)
        {
            if (type == GimDataClassType)
                return GimDataInterfaceType;
            if (type == GimElementClassType)
                return GimElementInterfaceType;
            if (type == GimLocalClassType)
                return GimLocalInterfaceType;
            if (type == GimObjectClassType)
                return GimObjectInterfaceType;

            return type;
        }
        

        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            var superType = GetGimInterface(type);
            if(type == superType)
                return base.CreateProperties(type, memberSerialization);

            // Only include the properties declared in the interface
            var props = base.CreateProperties(superType, memberSerialization);

            // Map those properties to the actual instance's property getters
            var actualProps = base.CreateProperties(type, memberSerialization);
            foreach (var prop in props)
            {
                var match = actualProps.FirstOrDefault(p => p.PropertyName == prop.PropertyName);
                if (match != null)
                {
                    prop.ValueProvider = match.ValueProvider;
                }
            }

            return props;
        }

    }
}
