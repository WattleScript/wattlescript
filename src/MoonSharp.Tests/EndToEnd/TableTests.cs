﻿using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter.Execution;
using NUnit.Framework;
using MoonSharp.Interpreter.CoreLib;

namespace MoonSharp.Interpreter.Tests.EndToEnd
{
	[TestFixture]
	public class TableTests
	{
		[Test]
		public void TableAccessAndEmptyCtor()
		{
			string script = @"
						a = { }
						
						a[1] = 1;

						return a[1]";

			DynValue res = Script.RunString(script);

			Assert.AreEqual(DataType.Number, res.Type);
			Assert.AreEqual(1, res.Number);
		}



		[Test]
		public void TableAccessAndCtor()
		{
			string script = @"
						a = { 55, 2, 3, aurevoir=6, [false] = 7 }
						
						a[1] = 1;
						a.ciao = 4;
						a['hello'] = 5;

						return a[1], a[2], a[3], a['ciao'], a.hello, a.aurevoir, a[false]";

			DynValue res = Script.RunString(script);

			Assert.AreEqual(DataType.Tuple, res.Type);
			Assert.AreEqual(7, res.Tuple.Length);
			Assert.AreEqual(DataType.Number, res.Tuple[0].Type);
			Assert.AreEqual(DataType.Number, res.Tuple[1].Type);
			Assert.AreEqual(DataType.Number, res.Tuple[2].Type);
			Assert.AreEqual(DataType.Number, res.Tuple[3].Type);
			Assert.AreEqual(DataType.Number, res.Tuple[4].Type);
			Assert.AreEqual(DataType.Number, res.Tuple[5].Type);
			Assert.AreEqual(DataType.Number, res.Tuple[6].Type);
			Assert.AreEqual(1, res.Tuple[0].Number);
			Assert.AreEqual(2, res.Tuple[1].Number);
			Assert.AreEqual(3, res.Tuple[2].Number);
			Assert.AreEqual(4, res.Tuple[3].Number);
			Assert.AreEqual(5, res.Tuple[4].Number);
			Assert.AreEqual(6, res.Tuple[5].Number);
			Assert.AreEqual(7, res.Tuple[6].Number);
		}

		[Test]
		public void TableMethod1()
		{
			string script = @"
						x = 0
	
						a = 
						{ 
							value = 1912,

							val = function(self, num)
								x = self.value + num
							end
						}
						
						a.val(a, 82);

						return x";

			DynValue res = Script.RunString(script);

			Assert.AreEqual(DataType.Number, res.Type);
			Assert.AreEqual(1994, res.Number);
		}

		[Test]
		public void TableMethod2()
		{
			string script = @"
						x = 0
	
						a = 
						{ 
							value = 1912,

							val = function(self, num)
								x = self.value + num
							end
						}
						
						a:val(82);

						return x";

			DynValue res = Script.RunString(script);

			Assert.AreEqual(DataType.Number, res.Type);
			Assert.AreEqual(1994, res.Number);
		}

		[Test]
		public void TableMethod3()
		{
			string script = @"
						x = 0
	
						a = 
						{ 
							value = 1912,
						}

						function a.val(self, num)
							x = self.value + num
						end
						
						a:val(82);

						return x";

			DynValue res = Script.RunString(script);

			Assert.AreEqual(DataType.Number, res.Type);
			Assert.AreEqual(1994, res.Number);
		}


		[Test]
		public void TableMethod4()
		{
			string script = @"
						x = 0
	
						local a = 
						{ 
							value = 1912,
						}

						function a:val(num)
							x = self.value + num
						end
						
						a:val(82);

						return x";

			DynValue res = Script.RunString(script);

			Assert.AreEqual(DataType.Number, res.Type);
			Assert.AreEqual(1994, res.Number);
		}

		[Test]
		public void TableMethod5()
		{
			string script = @"
						x = 0

						a = 
						{ 
							value = 1912,
						}

						b = { tb = a };
						c = { tb = b };

						function c.tb.tb:val(num)
							x = self.value + num
						end
						
						a:val(82);

						return x";

			DynValue res = Script.RunString(script);

			Assert.AreEqual(DataType.Number, res.Type);
			Assert.AreEqual(1994, res.Number);
		}

		
		[Test]
		public void TableMethod6()
		{
			string script = @"
						do
						  local a = {x=0}
						  function a:add (x) self.x, a.y = self.x+x, 20; return self end
						  return (a:add(10):add(20):add(30).x == 60 and a.y == 20)
						end";

			DynValue res = Script.RunString(script);

			Assert.AreEqual(DataType.Boolean, res.Type);
			Assert.AreEqual(true, res.CastToBool());
		}

		[Test]
		public void TableNextWithChangeInCollection()
		{
			string script = @"
				x = { }

				function copy(k, v)
					x[k] = v;
				end


				t = 
				{
					a = 1,
					b = 2,
					c = 3,
					d = 4,
					e = 5
				}

				k,v = next(t, nil);
				copy(k, v);

				k,v = next(t, k);
				copy(k, v);
				v = nil;

				k,v = next(t, k);
				copy(k, v);

				k,v = next(t, k);
				copy(k, v);

				k,v = next(t, k);
				copy(k, v);

				s = x.a .. '|' .. x.b .. '|' .. x.c .. '|' .. x.d .. '|' .. x.e

				return s;";

			DynValue res = Script.RunString(script);

			Assert.AreEqual(DataType.String, res.Type);
			Assert.AreEqual("1|2|3|4|5", res.String);
		}


		[Test]
		public void TablePairsWithoutMetatable()
		{
			string script = @"
				V = 0
				K = ''

				t = 
				{
					a = 1,
					b = 2,
					c = 3,
					d = 4,
					e = 5
				}

				for k, v in pairs(t) do
					K = K .. k;
					V = V + v;
				end

				return K, V;";

			DynValue res = Script.RunString(script);

			Assert.AreEqual(DataType.Tuple, res.Type);
			Assert.AreEqual(DataType.String, res.Tuple[0].Type);
			Assert.AreEqual(DataType.Number, res.Tuple[1].Type);
			Assert.AreEqual(5, res.Tuple[0].String.Length);
			Assert.AreEqual(15, res.Tuple[1].Number);
		}

		[Test]
		public void TableIPairsWithoutMetatable()
		{
			string script = @"    
				x = 0
				y = 0

				t = { 2, 4, 6, 8, 10, 12 };

				for i,j in ipairs(t) do
					x = x + i
					y = y + j

					if (i >= 3) then
						break
					end
				end
    
				return x, y";

			DynValue res = Script.RunString(script);

			Assert.AreEqual(DataType.Tuple, res.Type);
			Assert.AreEqual(2, res.Tuple.Length);
			Assert.AreEqual(DataType.Number, res.Tuple[0].Type);
			Assert.AreEqual(DataType.Number, res.Tuple[1].Type);
			Assert.AreEqual(6, res.Tuple[0].Number);
			Assert.AreEqual(12, res.Tuple[1].Number);
		}

		[Test]
		public void TestLoadSyntaxError()
		{
			string script = @"    
			function reader ()
				i = i + 1
				return t[i]
			end


			t = { [[?syntax error?]] }
			i = 0
			f, msg = load(reader, 'errorchunk')

			return f, msg;
		";

			DynValue res = Script.RunString(script);

			Assert.AreEqual(DataType.Tuple, res.Type);
			Assert.AreEqual(2, res.Tuple.Length);
			Assert.AreEqual(DataType.Nil, res.Tuple[0].Type);
			Assert.AreEqual(DataType.String, res.Tuple[1].Type);
		}


		[Test]
		public void TableSimplifiedAccess1()
		{
			string script = @"    
			t = {
				ciao = 'hello'
			}

			return t;
		";

			Script s = new Script();
			DynValue t = s.DoString(script);

			Assert.AreEqual("hello", t.Table["ciao"]);
		}

		[Test]
		public void TableSimplifiedAccess2()
		{
			string script = @"    
			t = {
				ciao = x
			}

			return t;
		";

			Script s = new Script();
			s.Globals["x"] = "hello";
			DynValue t = s.DoString(script);

			Assert.AreEqual("hello", t.Table["ciao"]);
		}

		[Test]
		public void TableSimplifiedAccess3()
		{
			string script = @"    
			t = {
			}

			return t;
		";

			Script s = new Script();
			DynValue t = s.DoString(script);

			s.Globals["t", "ciao"] = "hello";

			Assert.AreEqual("hello", t.Table["ciao"]);
		}

		[Test]
		public void TableSimplifiedAccess4()
		{
			string script = @"    
			t = {
			}
		";

			Script s = new Script();
			s.DoString(script);

			s.Globals["t", "ciao"] = "hello";

			Assert.AreEqual("hello", s.Globals["t", "ciao"]);
		}


		[Test]
		public void TableSimplifiedAccess5()
		{
			string script = @"    
			t = {
				ciao = 'hello'
			}
		";

			Script s = new Script();
			s.DoString(script);

			Assert.AreEqual("hello", s.Globals["t", "ciao"]);
		}

		[Test]
		public void TableSimplifiedAccess6()
		{
			string script = @"    
			t = {
				ciao = 
				{	'hello' }
			}
		";

			Script s = new Script(CoreModules.None);
			s.DoString(script);

			Assert.AreEqual("hello", s.Globals["t", "ciao", 1]);
		}


		[Test]
		public void TestNilRemovesEntryForPairs()
		{
			string script = @"
				str = ''

				function showTable(t)
					for i, j in pairs(t) do
						str = str .. i;
					end
					str = str .. '$'
				end

				tb = {}
				tb['id'] = 3

				showTable(tb)

				tb['id'] = nil

				showTable(tb)

				return str
			";

			DynValue res = Script.RunString(script);

			Assert.AreEqual(DataType.String, res.Type);
			Assert.AreEqual("id$$", res.String);
		}

		[Test]
		public void TestUnpack()
		{
			string script = @"
				return unpack({3,4})
			";

			DynValue res = Script.RunString(script);

			Assert.AreEqual(DataType.Tuple, res.Type);
			Assert.AreEqual(2, res.Tuple.Length);
			Assert.AreEqual(3, res.Tuple[0].Number);
			Assert.AreEqual(4, res.Tuple[1].Number);
		}

		[Test]
		public void PrimeTable_1()
		{
			string script = @"    
			t = ${
				ciao = 'hello'
			}
		";

			Script s = new Script();
			s.DoString(script);

			Assert.AreEqual("hello", s.Globals["t", "ciao"]);
			Assert.IsTrue(s.Globals.Get("t").Table.OwnerScript == null);
		}


		[Test]
		public void PrimeTable_2()
		{
			Assert.Throws<ScriptRuntimeException>(() =>
			{
				string script = @"    
				t = ${
					ciao = function() end
				}";

				Script s = new Script();
				s.DoString(script);

				Assert.Fail();
			});
		}


		[Test]
		public void Table_Length_Calculations()
		{
			Table T = new Table(null);

			Assert.AreEqual(0, T.Length, "A");

			T.Set(1, DynValue.True);

			Assert.AreEqual(1, T.Length, "B");

			T.Set(2, DynValue.True);
			T.Set(3, DynValue.True);
			T.Set(4, DynValue.True);

			Assert.AreEqual(4, T.Length, "C");

			T.Set(3, DynValue.Nil);

			Assert.AreEqual(2, T.Length, "D");

			T.Set(3, DynValue.True);

			Assert.AreEqual(4, T.Length, "E");

			T.Set(3, DynValue.Nil);

			Assert.AreEqual(2, T.Length, "F");

			T.Append(DynValue.True);

			Assert.AreEqual(4, T.Length, "G");

			T.Append(DynValue.True);

			Assert.AreEqual(5, T.Length, "H");

		}

		[Test]
		public void Table_ZeroIndexed_Length_Calculations()
		{
			Script s = new Script();
			s.Options.IndexTablesFrom = 0;
			Table t = new Table(s);

			Assert.AreEqual(0, t.Length, "A");

			t.Append(1);

			Assert.AreEqual(1, t.Get(0).CastToInt(), "B1");
			Assert.AreEqual(1, t.Length, "B2");

			t.Append(5);

			Assert.AreEqual(1, t.Get(0).CastToInt(), "C1");
			Assert.AreEqual(5, t.Get(1).CastToInt(), "C2");
			Assert.AreEqual(2, t.Length, "C3");

			t.Set(0, 20);
			t.Set(1, 21);

			Assert.AreEqual(2, t.Length, "D1");
			Assert.AreEqual(20, t.Get(0).CastToInt(), "D2");
			Assert.AreEqual(21, t.Get(1).CastToInt(), "D3");
		}

		[Test]
		public void Table_ZeroIndexed_Append()
		{
			Script s = new Script();
			s.Options.IndexTablesFrom = 0;
			Table t = new Table(s);

			t.Append("STR1");
			t.Append("STR2");
			t.Append("STR3");

			List<string> vals = t.Pairs.Select(x => x.Value.String).ToList();

			Assert.AreEqual(vals.Count, 3, "A");
			Assert.AreEqual("STR1", vals[0], "B");
			Assert.AreEqual("STR2", vals[1], "C");
			Assert.AreEqual("STR3", vals[2], "D");
		}

		[Test]
		public void Table_ZeroIndexed_Remove()
		{
			Script s = new Script();
			s.Options.IndexTablesFrom = 0;
			Table t = new Table(s);

			t.Append("STR1");
			t.Remove(0);

			Assert.AreEqual(0, t.Length, "A");
		}
	}
}
