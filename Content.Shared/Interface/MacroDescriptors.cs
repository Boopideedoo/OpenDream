﻿using System.Collections.Generic;

namespace Content.Shared.Interface {
    public class MacroSetDescriptor {
        public string Name;
        public List<MacroDescriptor> Macros;

        public MacroSetDescriptor(string name, List<MacroDescriptor> macros) {
            Name = name;
            Macros = macros;
        }
    }

    public class MacroDescriptor : ElementDescriptor {
        public string Id {
            get => _id ?? Command;
            set => _id = value;
        }
        private string _id;

        [InterfaceAttribute("command")]
        public string Command;

        public MacroDescriptor(string id) : base(null) {
            Id = id;
        }
    }
}
