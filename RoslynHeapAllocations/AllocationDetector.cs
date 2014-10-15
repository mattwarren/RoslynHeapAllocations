using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Collections.Generic;
using System.Linq;

namespace RoslynHeapAllocations
{
    internal static class AllocationDetector
    {
        /// <summary>
        /// Very good reference http://stackoverflow.com/questions/7995606/boxing-occurrence-in-c-sharp
        /// and http://msdn.microsoft.com/en-us/magazine/cc163930.aspx
        /// Also see http://stackoverflow.com/questions/1381370/hidden-boxing-in-the-bcl/1382033#1382033
        /// and http://blogs.msdn.com/b/ricom/archive/2007/01/26/performance-quiz-12-the-cost-of-a-good-hash-solution.aspx
        /// </summary>
        /// <param name="instructions"></param>
        /// <returns></returns>
        internal static AllocationType DetectCodeGroupAllocations(List<Instruction> instructions)
        {
            if (instructions.All(i => i.OpCode == OpCodes.Nop))
                return AllocationType.None;

            TypeDefinition constrainedTypeDefinition = null;
            foreach (var instruction in instructions)
            {
                var method = instruction.Operand as MethodReference;
                
                // Do the simple tests first
                if (instruction.OpCode == OpCodes.Newarr)
                {
                    return AllocationType.New;
                }
                else if (instruction.OpCode == OpCodes.Box)
                {
                    // See http://blog.nerdbank.net/2012/06/gc-pressure-series-hidden-boxing.html
                    return AllocationType.Boxing;
                }
                else if (instruction.OpCode == OpCodes.Constrained)
                {
                    constrainedTypeDefinition = instruction.Operand as TypeDefinition;
                }
                // Now the more complex ones
                else if (instruction.OpCode == OpCodes.Newobj)
                {
                    // Fall-back, just set it an a "New"
                    if (method == null)
                        return AllocationType.New;

                    // Value types (structs) are normally allocated via "initobj", but just in case we do a double-check
                    // See http://stackoverflow.com/questions/15207683/why-c-sharp-compiler-in-some-cases-emits-newobj-stobj-rather-than-call-instance
                    // and http://stackoverflow.com/questions/11966930/difference-between-call-instance-vs-newobj-instance-in-il
                    if (method.MethodReturnType.ReturnType.IsValueType == false)
                        return AllocationType.New;
                }
                else if (instruction.OpCode == OpCodes.Callvirt)
                {
                    var allocationType = DetectAllocationsInVirtualMethodCalls(method, constrainedTypeDefinition);
                    if (allocationType != AllocationType.None)
                        return allocationType;
                    // Otherwise we carry on, in a later instruction causes an allocation
                }
            }

            return AllocationType.None;
        }

        private static AllocationType DetectAllocationsInVirtualMethodCalls(MethodReference method, TypeDefinition constrainedTypeDefinition)
        {
            if (method == null)
                return AllocationType.None;

            const string IEnumerableName = "System.Collections.Generic.IEnumerable";

            // See http://blog.nerdbank.net/2012/06/gc-pressure-series-introduction-and.html
            if (method.FullName.Contains(IEnumerableName) &&
                method.FullName.Contains("GetEnumerator()") &&
                method.MethodReturnType.ReturnType.IsValueType == false)
            {
                return AllocationType.GetEnumerator;
            }
            else if (method.FullName.Contains(IEnumerableName) &&
                     method.FullName.Contains("MoveNext()") &&
                     method.MethodReturnType.ReturnType.IsValueType == false)
            {
                //TODO this isn't the right check, I think we need to check if the MoveNext is called on a Value Type (i.e. if what GetEnumerator returned)
            }
// ReSharper disable ConditionIsAlwaysTrueOrFalse
            else if ((method.FullName.Contains("System.Object::GetHashCode()") ||
                      method.FullName.Contains("System.Object::ToString()") ||
                      method.FullName.Contains("System.Object::Equals(System.Object)")) &&
                     constrainedTypeDefinition != null)
// ReSharper restore ConditionIsAlwaysTrueOrFalse
// ReSharper disable HeuristicUnreachableCode
            {
                var isGetHashCodeOverridden = DetectGetHashCodeOverridden(constrainedTypeDefinition,
                                                                                method.FullName,
                                                                                method.ReturnType);
                if (isGetHashCodeOverridden == false)
                    return AllocationType.Boxing;
            }
// ReSharper restore HeuristicUnreachableCode
            return AllocationType.None;
        }

        private static bool DetectGetHashCodeOverridden(TypeDefinition constrainedTypeDefinition, string expectedMethodName, TypeReference expectedReturnType)
        {
            //TestingResharperMemoryPlugin.cs:34 Boxing allocation: inherited System.Object virtual method call on value type instance
            // From http://msdn.microsoft.com/en-us/library/system.reflection.emit.opcodes.constrained(v=vs.110).aspx
            // - If thisType is a value type and thisType does not implement method then ptr is dereferenced, boxed, 
            //   and passed as the 'this' pointer to the callvirt  method instruction.
            // This last case can occur only when method was defined on Object, ValueType, or Enum and not overridden by thisType. 
            // In this case, the boxing causes a copy of the original object to be made. However, because none of the methods of Object, 
            // ValueType, and Enum modify the state of the object, this fact cannot be detected.
            // Also see http://www.manning-sandbox.com/message.jspa?messageID=143693#143694 and http://www.pvle.be/tag/callvirt/

            foreach (var methodDefinition in constrainedTypeDefinition.Methods)
            {
                //if ((methodDefinition.Name == "GetHashCode" || methodDefinition.Name == "ToString") &&
                if (expectedMethodName.Contains(methodDefinition.Name) &&
                    methodDefinition.Parameters.Count == 0 &&
                    methodDefinition.IsConstructor == false &&
                    methodDefinition.IsPublic &&
                    methodDefinition.IsDefinition &&
                    methodDefinition.IsVirtual && // Virtual seems the be the key one, can't override a non-virtual method
                    methodDefinition.ReturnType == expectedReturnType)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
