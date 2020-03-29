using MG.Posh.Extensions.Bound;
using NRP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Management.Automation;

namespace MG.NewRandomPassword
{
    [Cmdlet(VerbsCommon.New, "RandomPassword", ConfirmImpact = ConfirmImpact.None, DefaultParameterSetName = "StaticLength")]
    [OutputType(typeof(string), typeof(byte[]))]
    [CmdletBinding(PositionalBinding = false)]
    public class NewRandomPassword : PSCmdlet
    {
        #region FIELDS/CONSTANTS


        #endregion

        #region PARAMETERS
        [Parameter(Mandatory = false, Position = 0, ParameterSetName = "StaticLength")]
        [Alias("Length")]
        [ValidateRange(4, 100000)]
        public int PasswordLength { get; set; } = 8;

        [Parameter(Mandatory = true, ParameterSetName = "DynamicLength")]
        [Alias("max")]
        [ValidateRange(5, 100000)]
        public int MaxLength { get; set; }

        [Parameter(Mandatory = true, ParameterSetName = "DynamicLength")]
        [Alias("min")]
        [ValidateRange(4, 99999)]
        public int MinLength { get; set; }

        [Parameter(Mandatory = false)]
        public int Count { get; set; } = 1;

        [Parameter(Mandatory = false)]
        [ValidateSet(
            Characters.AZ_LOWERCASE,
            Characters.AZ_UPPERCASE,
            Characters.NUMBERS_1_0,
            Characters.NUMBERS_2_0,
            Characters.NUMBERS_2_9,
            Characters.SYMBOLS_1,
            Characters.SYMBOLS_2
        )]
        public string[] InputStrings { get; set; }

        [Parameter(Mandatory = false)]
        public char FirstCharacter { get; set; }

        #endregion

        #region CMDLET PROCESSING
        protected override void BeginProcessing()
        {
            if (this.Count <= 0)
                this.Count = 1;
        }

        protected override void ProcessRecord()
        {
            IPasswordGenerator pg = null;
            if (this.ContainsParameter(x => x.InputStrings))
            {
                pg = PasswordGenerator.Create(this.PasswordLength, this.InputStrings);
            }
            else
            {
                pg = PasswordGenerator.Create(this.PasswordLength);
            }

            pg.NumberToCreate = this.Count;
            if (this.ContainsAllParameters(x => x.MaxLength, x => x.MinLength))
            {
                pg.MaxLength = this.MaxLength;
                pg.MinLength = this.MinLength;
            }

            if (this.ContainsParameter(x => x.FirstCharacter))
            {
                pg.FirstCharacter = this.FirstCharacter;
            }

            base.WriteObject(pg.Generate(), true);
        }

        #endregion

        #region BACKEND METHODS


        #endregion
    }
}