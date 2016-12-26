﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Common.Core;
using static System.FormattableString;

namespace Microsoft.R.Host.Client.API {
    public static class Converter {
        public static List<T> ToListOf<T>(this IEnumerable<object> e) {
            return new List<T>(e.Select(x => (T)Convert.ChangeType(x, typeof(T))));
        }

        public static string ToRLiteral(this object value) {
            if (value == null) {
                return "NULL";
            }

            string rvalue;
            var t = value.GetType();
            if (t == typeof(int) || t == typeof(long) || t == typeof(uint) || t == typeof(ulong) || t == typeof(float) || t == typeof(double)) {
                rvalue = Invariant($"{value}");
            } else if (t == typeof(bool)) {
                rvalue = ((bool)value) ? "TRUE" : "FALSE";
            } else if (t == typeof(string)) {
                var s = (string)value;
                if (s.EqualsOrdinal("...")) {
                    return "...";
                }
                rvalue = Invariant($"{s.ToRStringLiteral()}");
            } else if (t == typeof(char)) {
                rvalue = Invariant($"'{(char)value}'");
            } else if (t == typeof(RObject)) {
                rvalue = ((RObject)value).Name;
            } else if (value is IEnumerable<object>) {
                rvalue = ((IEnumerable<object>)value).ToRListConstructor();
            } else {
                throw new ArgumentException(Invariant($"Unsupported value type of {nameof(value)}: {t}"));
            }
            return rvalue;
        }

        public static string ToRListConstructor<T>(this IEnumerable<T> list) {
            var sb = new StringBuilder();
            foreach (var o in list) {
                if (sb.Length == 0) {
                    sb.Append("c(");
                } else {
                    sb.Append(", ");
                }
                var s = o.ToRLiteral();
                sb.Append(s);
            }
            sb.Append(')');
            return sb.ToString();
        }

        public static string ToRDataFrameConstructor(this DataFrame df) {
            var sb = new StringBuilder();
            foreach (var list in df.Data) {
                if (sb.Length == 0) {
                    sb.Append("data.frame(");
                } else {
                    sb.Append(", ");
                }
                sb.Append(list.ToRListConstructor());
            }
            sb.Append(", stringsAsFactors=FALSE)");
            return sb.ToString();
        }

        public static string ToRFunctionCall(this string function, params object[] arguments) {
            var sb = new StringBuilder(function);
            sb.Append('(');

            // Construct argument list
            foreach (var arg in arguments) {
                if (sb.Length > function.Length + 1) {
                    sb.Append(", ");
                }
                if (arg is RFunctionArg) {
                    var r = (RFunctionArg)arg;
                    if (!string.IsNullOrEmpty(r.Name)) {
                        sb.Append(r.Name);
                        sb.Append(" = ");
                    }
                    sb.Append(r.Value);
                } else {
                    sb.Append($"{arg.ToRLiteral()}");
                }
            }
            sb.Append(')');
            return sb.ToString();
        }
    }
}
