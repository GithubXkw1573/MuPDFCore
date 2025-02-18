﻿/*
    MuPDFCore - A set of multiplatform .NET Core bindings for MuPDF.
    Copyright (C) 2024  Giorgio Bianchini

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as
    published by the Free Software Foundation, version 3.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace MuPDFCore
{
    /// <summary>
    /// Represents a document outline (table of contents).
    /// </summary>
    public class MuPDFOutline : IReadOnlyList<MuPDFOutlineItem>
    {
        /// <summary>
        /// The outline items.
        /// </summary>
        public IReadOnlyList<MuPDFOutlineItem> Items { get; }

        /// <summary>
        /// Load the outline for a document.
        /// </summary>
        /// <param name="context">A <see cref="MuPDFContext"/> to store resources and the exception stack.</param>
        /// <param name="document">The document whose outline should be loaded.</param>
        internal MuPDFOutline(MuPDFContext context, MuPDFDocument document) 
        {
            IntPtr outline = NativeMethods.LoadOutline(context.NativeContext, document.NativeDocument);

            if (outline != IntPtr.Zero)
            {
                this.Items = MuPDFOutlineItem.TraverseOutline(outline).ToArray();
                NativeMethods.DisposeOutline(context.NativeContext, outline);
            }
            else
            {
                this.Items = new MuPDFOutlineItem[0];
            }
            
        }

        /// <inheritdoc />
        public int Count => Items.Count;

        /// <inheritdoc />
        public MuPDFOutlineItem this[int index] => Items[index];

        /// <inheritdoc />
        public IEnumerator<MuPDFOutlineItem> GetEnumerator()
        {
            return Items.GetEnumerator();
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)Items).GetEnumerator();
        }
    }

    /// <summary>
    /// Represents an item in a document outline (table of contents).
    /// </summary>
    public class MuPDFOutlineItem
    {
        /// <summary>
        /// The title of the item. This can be null if the item does not have any text.
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// The destination in the document to be displayed when this outline item is activated. This can be null if the item has no destination.
        /// </summary>
        public string Uri { get; }

        /// <summary>
        /// Locations within the document are referred to in terms of chapter and page, rather than just a page number. For some documents (such as epub documents with large numbers of pages broken into many chapters) this can make navigation much faster as only the required chapter needs to be decoded at a time.
        /// </summary>
        public int Chapter { get; }

        /// <summary>
        /// The page number of an internal link, or -1 for external links or links with no destination.
        /// </summary>
        public int Page { get; }

        /// <summary>
        /// The location on the page of the item pointed to by this outline item.
        /// </summary>
        public PointF Location { get; }

        /// <summary>
        /// The sub items of this outline item (may be empty, but will not be null).
        /// </summary>
        public IReadOnlyList<MuPDFOutlineItem> Children { get; private set; }

        [StructLayout(LayoutKind.Sequential)]
        private struct fz_outline
        {
            public int refs;
            public IntPtr title;
            public IntPtr uri;
            public int chapter;
            public int page;
            public float x;
            public float y;
            public IntPtr next;
            public IntPtr down;
            public int is_open;
        }

        /// <summary>
        /// Get the length of a null-terminated C string.
        /// </summary>
        /// <param name="stringAddress">A pointer to the string.</param>
        /// <returns>The length of the string in bytes.</returns>
        private static unsafe int strlen(byte* stringAddress)
        {
            byte* originalAddress = stringAddress;

            while (*stringAddress != 0)
            {
                stringAddress++;
            }

            return (int)(stringAddress - originalAddress);
        }

        /// <summary>
        /// Convert a null-terminated C string in UTF8 encoding to a .NET <see langword="string"/>.
        /// </summary>
        /// <param name="stringAddress">A pointer to the string.</param>
        /// <returns>A .NET <see langword="string"/>.</returns>
        private static unsafe string PtrToStringUTF8(byte* stringAddress) => Encoding.UTF8.GetString(stringAddress, strlen(stringAddress));

        /// <summary>
        /// Create a new <see cref="MuPDFOutlineItem"/> from a native pointer.
        /// </summary>
        /// <param name="nativeOutlineItem">The pointer to the native outline item.</param>
        /// <param name="next">A pointer to the next item in the outline. This will be <see cref="IntPtr.Zero"/> for the last item.</param>
        private unsafe MuPDFOutlineItem(IntPtr nativeOutlineItem, out IntPtr next)
        {
            fz_outline nativeItem = Marshal.PtrToStructure<fz_outline>(nativeOutlineItem);

            string title = nativeItem.title == IntPtr.Zero ? null : PtrToStringUTF8((byte*)nativeItem.title);
            string uri = nativeItem.uri == IntPtr.Zero ? null : PtrToStringUTF8((byte*)nativeItem.uri);

            this.Title = title;
            this.Uri = uri;
            this.Chapter = nativeItem.chapter;
            this.Page = nativeItem.page;
            this.Location = new PointF(nativeItem.x, nativeItem.y);

            next = nativeItem.next;

            if (nativeItem.down != IntPtr.Zero)
            {
                this.Children = TraverseOutline(nativeItem.down).ToArray();
            }
            else
            {
                this.Children = new MuPDFOutlineItem[0];
            }
        }

        /// <summary>
        /// Recursively traverse the outline, loading information about all the items.
        /// </summary>
        /// <param name="firstOutlineItem">The first item in the outline.</param>
        /// <returns>A collection of <see cref="MuPDFOutlineItem"/> containing all the information in the outline.</returns>
        internal static IEnumerable<MuPDFOutlineItem> TraverseOutline(IntPtr firstOutlineItem)
        {
            IntPtr currOutlineItem = firstOutlineItem;

            while (currOutlineItem != IntPtr.Zero)
            {
                MuPDFOutlineItem item = new MuPDFOutlineItem(currOutlineItem, out IntPtr next);
                yield return item;
                currOutlineItem = next;
            }
        }
    }

}
