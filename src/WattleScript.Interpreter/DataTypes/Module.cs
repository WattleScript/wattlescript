﻿using System.IO;

namespace WattleScript.Interpreter
{
    public class Module
    {
        public static Module LocalModule = new Module(true);

        /// <summary>
        /// Gets/sets the Wattlescript source code of this module 
        /// </summary>
        public string Code { get; set; }
        
        /// <summary>
        /// Gets/sets the Wattlescript bytecode of this module. If not null, this has priority over <see cref="Code"/>
        /// </summary>
        public byte[] Bytecode { get; set; }
        
        /// <summary>
        /// Gets/sets the Stream with Wattlescript bytecode of this module. If not null, this has priority over <see cref="Bytecode"/>
        /// </summary>
        public Stream Stream { get; set; }
        
        internal bool IsEntryRef { get; set; }

        internal Module(bool isEntryRef)
        {
            IsEntryRef = isEntryRef;
        }
        
        public Module(string code)
        {
            Code = code;
        }

        public Module(byte[] bytecode)
        {
            Bytecode = bytecode;
        }

        public Module(Stream stream)
        {
            Stream = stream;
        }
    }
}