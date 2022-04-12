namespace WattleScript.Interpreter
{
    public enum AnnotationValueParsingPolicy
    {
        /// <summary>
        /// Annotations are parsed as string @MyAnnotation("strValue") or as a table @MyAnnotation({"key1", "key2"})
        /// </summary>
        StringOrTable,
        /// <summary>
        /// Annotations are always parsed as a table and curly brackes enclosing the table are relaxed.
        /// In this mode @MyAnnotation("key1", "key2") is equivalent to @MyAnnotation({"key1", "key2"})
        /// </summary>
        ForceTable
    }
    
    public enum AnnotationAction
    {
        Allow,
        Ignore,
        Error
    }
    
    public interface IAnnotationPolicy
    {
        AnnotationAction OnChunkAnnotation(string name, DynValue value);
        AnnotationAction OnFunctionAnnotation(string name, DynValue value);
        AnnotationValueParsingPolicy AnnotationParsingPolicy { get; set; }
    }

    public class CustomPolicy : IAnnotationPolicy
    {
        public CustomPolicy(AnnotationValueParsingPolicy parsingPolicy)
        {
            AnnotationParsingPolicy = parsingPolicy;
        } 
            
        public AnnotationAction OnChunkAnnotation(string name, DynValue value)
        {
            return AnnotationAction.Allow;
        }

        public AnnotationAction OnFunctionAnnotation(string name, DynValue value)
        {
            return AnnotationAction.Allow;
        }

        public AnnotationValueParsingPolicy AnnotationParsingPolicy { get; set; }
    }
    
    public static class AnnotationPolicies
    {
        public static IAnnotationPolicy Allow { get; } = new AllowPolicy();

        public static IAnnotationPolicy Ignore { get; } = new IgnorePolicy();

        public static IAnnotationPolicy Error { get; } = new ErrorPolicy();

        class AllowPolicy : IAnnotationPolicy
        {
            public AnnotationAction OnChunkAnnotation(string name, DynValue value)
            {
                return AnnotationAction.Allow;
            }

            public AnnotationAction OnFunctionAnnotation(string name, DynValue value)
            {
                return AnnotationAction.Allow;
            }

            public AnnotationValueParsingPolicy AnnotationParsingPolicy { get; set; } = AnnotationValueParsingPolicy.StringOrTable;
        }
        
        class IgnorePolicy : IAnnotationPolicy
        {
            public AnnotationAction OnChunkAnnotation(string name, DynValue value)
            {
                return AnnotationAction.Ignore;
            }

            public AnnotationAction OnFunctionAnnotation(string name, DynValue value)
            {
                return AnnotationAction.Ignore;
            }

            public AnnotationValueParsingPolicy AnnotationParsingPolicy { get; set; } = AnnotationValueParsingPolicy.StringOrTable;
        }
        
        class ErrorPolicy : IAnnotationPolicy
        {
            public AnnotationAction OnChunkAnnotation(string name, DynValue value)
            {
                return AnnotationAction.Error;
            }

            public AnnotationAction OnFunctionAnnotation(string name, DynValue value)
            {
                return AnnotationAction.Error;
            }

            public AnnotationValueParsingPolicy AnnotationParsingPolicy { get; set; } = AnnotationValueParsingPolicy.StringOrTable;
        }
    }
}