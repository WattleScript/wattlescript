﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using WattleScript.Interpreter.Interop.BasicDescriptors;

namespace WattleScript.Interpreter.Interop
{
	/// <summary>
	/// Standard descriptor for userdata types.
	/// </summary>
	public class StandardUserDataDescriptor : DispatchingUserDataDescriptor, IWireableDescriptor
	{
		/// <summary>
		/// Gets the interop access mode this descriptor uses for members access
		/// </summary>
		public InteropAccessMode AccessMode { get; private set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="StandardUserDataDescriptor"/> class.
		/// </summary>
		/// <param name="type">The type this descriptor refers to.</param>
		/// <param name="accessMode">The interop access mode this descriptor uses for members access</param>
		/// <param name="friendlyName">A human readable friendly name of the descriptor.</param>
		public StandardUserDataDescriptor(Type type, InteropAccessMode accessMode, string friendlyName = null)
			: base(type, friendlyName)
		{
			if (accessMode == InteropAccessMode.NoReflectionAllowed)
				throw new ArgumentException("Can't create a StandardUserDataDescriptor under a NoReflectionAllowed access mode");

			if (Script.GlobalOptions.Platform.IsRunningOnAOT())
				accessMode = InteropAccessMode.Reflection;

			if (accessMode == InteropAccessMode.Default)
				accessMode = UserData.DefaultAccessMode;

			AccessMode = accessMode;

			FillMemberList();
		}

		/// <summary>
		/// Fills the member list.
		/// </summary>
		private void FillMemberList()
		{
			HashSet<string> membersToIgnore = new HashSet<string>(
				this.Type.GetCustomAttributes( typeof(WattleScriptHideMemberAttribute), true)
					.OfType<WattleScriptHideMemberAttribute>()
					.Select(a => a.MemberName)
				);

			Type type = this.Type;

			if (AccessMode == InteropAccessMode.HideMembers)
				return;

			if (!type.IsDelegateType())
			{
				// add declared constructors
				foreach (ConstructorInfo ci in type.GetAllConstructors())
				{
					if (membersToIgnore.Contains("__new"))
						continue;

					AddMember("__new", MethodMemberDescriptor.TryCreateIfVisible(ci, this.AccessMode));
				}

				// valuetypes don't reflect their empty ctor.. actually empty ctors are a perversion, we don't care and implement ours
				if (type.IsValueType && !membersToIgnore.Contains("__new"))
					AddMember("__new", new ValueTypeDefaultCtorMemberDescriptor(type));
			}


			// add methods to method list and metamethods
			foreach (MethodInfo mi in type.GetAllMethods())
			{
				if (membersToIgnore.Contains(mi.Name)) continue;

				MethodMemberDescriptor md = MethodMemberDescriptor.TryCreateIfVisible(mi, this.AccessMode);

				if (md != null)
				{
					if (!MethodMemberDescriptor.CheckMethodIsCompatible(mi, false))
						continue;

					// transform explicit/implicit conversions to a friendlier name.
					string name = mi.Name;
					if (mi.IsSpecialName && (mi.Name == SPECIALNAME_CAST_EXPLICIT || mi.Name == SPECIALNAME_CAST_IMPLICIT))
					{
						name = mi.ReturnType.GetConversionMethodName();
					}

					AddMember(name, md);

					foreach (string metaname in mi.GetMetaNamesFromAttributes())
					{
						AddMetaMember(metaname, md);
					}
				}
			}

			// get properties
			foreach (PropertyInfo pi in type.GetAllProperties())
			{
				if (pi.IsSpecialName || pi.GetIndexParameters().Any() || membersToIgnore.Contains(pi.Name))
					continue;

				AddMember(pi.Name, PropertyMemberDescriptor.TryCreateIfVisible(pi, this.AccessMode));
			}

			// get fields
			foreach (FieldInfo fi in type.GetAllFields())
			{
				if (fi.IsSpecialName || membersToIgnore.Contains(fi.Name))
					continue;

				AddMember(fi.Name, FieldMemberDescriptor.TryCreateIfVisible(fi, this.AccessMode));
			}

			// get events
			foreach (EventInfo ei in type.GetAllEvents())
			{
				if (ei.IsSpecialName || membersToIgnore.Contains(ei.Name))
					continue;

				AddMember(ei.Name, EventMemberDescriptor.TryCreateIfVisible(ei, this.AccessMode));
			}

			// get nested types and create statics
			foreach (Type nestedType in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
			{
				if (membersToIgnore.Contains(nestedType.Name))
					continue;

				if (!nestedType.IsGenericTypeDefinition)
				{
					if (nestedType.IsNestedPublic || nestedType.GetCustomAttributes(typeof(WattleScriptUserDataAttribute), true).Length > 0)
					{
						var descr = UserData.RegisterType(nestedType, this.AccessMode);

						if (descr != null)
							AddDynValue(nestedType.Name, UserData.CreateStatic(nestedType));
					}
				}
			}

			if (!membersToIgnore.Contains("[this]"))
			{
				if (Type.IsArray)
				{
					int rank = Type.GetArrayRank();

					ParameterDescriptor[] get_pars = new ParameterDescriptor[rank];
					ParameterDescriptor[] set_pars = new ParameterDescriptor[rank + 1];

					for (int i = 0; i < rank; i++)
						get_pars[i] = set_pars[i] = new ParameterDescriptor("idx" + i.ToString(), typeof(int));

					set_pars[rank] = new ParameterDescriptor("value", Type.GetElementType());

					AddMember(SPECIALNAME_INDEXER_SET, new ArrayMemberDescriptor(SPECIALNAME_INDEXER_SET, true, set_pars));
					AddMember(SPECIALNAME_INDEXER_GET, new ArrayMemberDescriptor(SPECIALNAME_INDEXER_GET, false, get_pars));
				}
				else if (Type == typeof(Array))
				{
					AddMember(SPECIALNAME_INDEXER_SET, new ArrayMemberDescriptor(SPECIALNAME_INDEXER_SET, true));
					AddMember(SPECIALNAME_INDEXER_GET, new ArrayMemberDescriptor(SPECIALNAME_INDEXER_GET, false));
				}
			}
		}




		public void PrepareForWiring(Table t)
		{
			if (AccessMode == InteropAccessMode.HideMembers || Type.Assembly == this.GetType().Assembly)
			{
				t.Set("skip", DynValue.NewBoolean(true));
			}
			else
			{
				t.Set("visibility", DynValue.NewString(this.Type.GetClrVisibility()));

				t.Set("class", DynValue.NewString(this.GetType().FullName));
				DynValue tm = DynValue.NewPrimeTable();
				t.Set("members", tm);
				DynValue tmm = DynValue.NewPrimeTable();
				t.Set("metamembers", tmm);

				Serialize(tm.Table, Members);
				Serialize(tmm.Table, MetaMembers);
			}
		}

		private void Serialize(Table t, IEnumerable<KeyValuePair<string, IMemberDescriptor>> members)
		{
			foreach (var pair in members)
			{
				IWireableDescriptor sd = pair.Value as IWireableDescriptor;

				if (sd != null)
				{
					DynValue mt = DynValue.NewPrimeTable();
					t.Set(pair.Key, mt);
					sd.PrepareForWiring(mt.Table);
				}
				else
				{
					t.Set(pair.Key, DynValue.NewString("unsupported member type : " + pair.Value.GetType().FullName));
				}
			}
		}
	}
}
