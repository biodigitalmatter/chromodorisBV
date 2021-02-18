/*
 *      ___  _  _  ____   __   _  _   __  ____   __  ____  __  ____
 *     / __)/ )( \(  _ \ /  \ ( \/ ) /  \(    \ /  \(  _ \(  )/ ___)
 *    ( (__ ) __ ( )   /(  O )/ \/ \(  O )) D ((  O ))   / )( \___ \
 *     \___)\_)(_/(__\_) \__/ \_)(_/ \__/(____/ \__/(__\_)(__)(____/BV
 *
 *    ChromodorisBV is built on Chromodoris
 *    (https://bitbucket.org/camnewnham/chromodoris) by Cameron Newnham,
 *    copyright 2015-2016. ChromodorisBV is copyright Anton Tetov Johansson
 *    2020.
 *
 *    This program is free software: you can redistribute it and/or modify
 *    it under the terms of the GNU General Public License as published by
 *    the Free Software Foundation, either version 3 of the License, or
 *    (at your option) any later version.
 *
 *    This program is distributed in the hope that it will be useful,
 *    but WITHOUT ANY WARRANTY; without even the implied warranty of
 *    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *    GNU General Public License for more details.
 *
 *    You should have received a copy of the GNU General Public License
 *    along with this program.  If not, see <http://www.gnu.org/licenses/>.
 *
 */

using System;
using System.Drawing;

using Chromodoris.Properties;

using Grasshopper.Kernel;

namespace Chromodoris
{
    public class ChromodorisBVInfo : GH_AssemblyInfo
    {
        #region Properties

        public override string Name => "ChromodorisBV";

        public override Bitmap Icon => Resources.Icons_Chromodoris;

        public override string Description =>
            //Return a short string describing the purpose of this GHA library.
            "A general purpose mesh library.";

        public override Guid Id => new Guid("24D9C88E-6A06-4572-9608-C20DDCBBF9AF");

        public override string AuthorName =>
            //Return a string identifying you or your company.
            "Anton Tetov Johansson";

        public override string AuthorContact =>
            //Return a string representing your preferred contact details.
            "anton@tetov.se";

        #endregion Properties
    }
}
