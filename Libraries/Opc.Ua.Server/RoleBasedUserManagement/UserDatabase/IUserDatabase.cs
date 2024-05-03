/* ========================================================================
 * Copyright (c) 2005-2020 The OPC Foundation, Inc. All rights reserved.
 *
 * OPC Foundation MIT License 1.00
 * 
 * Permission is hereby granted, free of charge, to any person
 * obtaining a copy of this software and associated documentation
 * files (the "Software"), to deal in the Software without
 * restriction, including without limitation the rights to use,
 * copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the
 * Software is furnished to do so, subject to the following
 * conditions:
 * 
 * The above copyright notice and this permission notice shall be
 * included in all copies or substantial portions of the Software.
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
 * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
 * OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
 * NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
 * HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
 * WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
 * OTHER DEALINGS IN THE SOFTWARE.
 *
 * The complete license agreement can be found here:
 * http://opcfoundation.org/License/MIT/1.00/
 * ======================================================================*/

using System;
using System.Collections.Generic;
using System.Security;

namespace Opc.Ua.Server.UserDatabase
{
    /// <summary>
    /// An abstract interface to the user database which stores logins with associated Roles
    /// </summary>
    public interface IUserDatabase
    {
        /// <summary>
        /// Initialize User Database
        /// </summary>
        void Initialize();
        /// <summary>
        /// Register new user
        /// </summary>
        /// <param name="userName">the username</param>
        /// <param name="password">the password</param>
        /// <param name="roles">the role of the new user</param>
        /// <returns>true if registered successfull</returns>
        bool CreateUser(string userName, string password, IEnumerable<Role> roles);
        /// <summary>
        /// Delete existing user
        /// </summary>
        /// <param name="userName">the user to delete</param>
        /// <returns>true if deleted successfully</returns>
        bool DeleteUser(string userName);
        /// <summary>
        /// checks if the provided credentials fit a user
        /// </summary>
        /// <param name="userName">the username</param>
        /// <param name="password">the password</param>
        /// <returns>true if userName + PW combination is correct</returns>
        bool CheckCredentials(string userName, string password);
        /// <summary>
        /// returns the Role of the provided user
        /// </summary>
        /// <param name="userName"></param>
        /// <returns>the Role of the provided users</returns>
        /// <exception cref="ArgumentException">When the user is not found</exception>
        IEnumerable<Role> GetUserRoles(string userName);
        /// <summary>
        /// changes the password of an existing users
        /// </summary>
        /// <param name="userName"></param>
        /// <param name="oldPassword"></param>
        /// <param name="newPassword"></param>
        /// <returns>true if change was successfull</returns>
        bool ChangePassword(string userName, string oldPassword, string newPassword);
    }
}
