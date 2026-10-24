namespace MG.Passwords;

/// <summary>
/// Provides the <c>New-RandomPassword</c> cmdlet, which generates random passwords of one length or of random lengths
/// within a range.
/// </summary>
/// <remarks>
/// <para>
/// A password holds a character from each group of characters when it has room for one, and a shorter password holds
/// characters from a random choice of the groups. Characters from all the groups fill the rest, and then everything is
/// shuffled except <see cref="FirstCharacter"/>, which always comes first. In the <c>RandomLength</c> and
/// <c>RandomLengthCharacterSet</c> parameter sets, each password gets a random length of its own.
/// </para>
/// <para>
/// <c>StaticLengthCharacterSet</c> and <c>RandomLengthCharacterSet</c> are twins of <c>StaticLength</c> and
/// <c>RandomLength</c> in which <see cref="CharacterSet"/> takes the place of <see cref="InputStrings"/>, so the two
/// parameters can't be used together.
/// </para>
/// <para>
/// An argument that is wrong on its own fails when PowerShell binds it, with an error that names its parameter, and
/// <see cref="CharacterSet"/> with <see cref="InputStrings"/> fails there too, because no parameter set has both. Other
/// arguments that conflict with each other end the statement from <see cref="BeginProcessing"/>, before any password
/// is generated, with an error in the <see cref="ErrorCategory.InvalidArgument"/> category.
/// </para>
/// </remarks>
[Cmdlet(VerbsCommon.New, "RandomPassword", DefaultParameterSetName = STATIC_LENGTH)]
[Alias("rpas")]
[OutputType(typeof(string))]
public sealed class NewRandomPasswordCommand : PSCmdlet
{
	// Each set of a length has a twin in which -CharacterSet takes the place of -InputStrings. Because it's mandatory
	// there, a call without it resolves to the set that it would with the two sets alone.
	private const string STATIC_LENGTH = "StaticLength";
	private const string STATIC_LENGTH_CHARACTER_SET = "StaticLengthCharacterSet";
	private const string RANDOM_LENGTH = "RandomLength";
	private const string RANDOM_LENGTH_CHARACTER_SET = "RandomLengthCharacterSet";

	// The longest password, a 2 MB string, far beyond any password or key. 1.2.0 accepted lengths up to int.MaxValue,
	// which no string can hold.
	private const int MAX_LENGTH = 1_048_576;

	private PasswordGenerator? _generator;

	/// <summary>
	/// Gets or sets the number of characters in each password.
	/// </summary>
	/// <remarks>
	/// The length includes <see cref="FirstCharacter"/> when it's given. The parameter belongs to the
	/// <c>StaticLength</c> parameter set, which is the default, and to its twin, <c>StaticLengthCharacterSet</c>.
	/// </remarks>
	/// <value>A length from 1 to 1,048,576. The default is 8.</value>
	[Parameter(Position = 0, ParameterSetName = STATIC_LENGTH)]
	[Parameter(Position = 0, ParameterSetName = STATIC_LENGTH_CHARACTER_SET)]
	[Alias("l", "length")]
	[ValidateRange(1, MAX_LENGTH)]
	public int PasswordLength { get; set; } = 8;

	/// <summary>
	/// Gets or sets the number of passwords to generate.
	/// </summary>
	/// <value>A count of 1 or more. The default is 1.</value>
	[Parameter(Position = 1)]
	[ValidateRange(1, int.MaxValue)]
	public int Count { get; set; } = 1;

	/// <summary>
	/// Gets or sets the least number of characters in each password.
	/// </summary>
	/// <remarks>
	/// Each password gets a length of its own, drawn at random from <see cref="MinimumLength"/> to
	/// <see cref="MaximumLength"/>, both included, and the length includes <see cref="FirstCharacter"/> when it's given.
	/// The parameter belongs to the <c>RandomLength</c> parameter set and to its twin, <c>RandomLengthCharacterSet</c>.
	/// </remarks>
	/// <value>A length from 1 to 1,048,575 that isn't greater than <see cref="MaximumLength"/>.</value>
	[Parameter(Mandatory = true, ParameterSetName = RANDOM_LENGTH)]
	[Parameter(Mandatory = true, ParameterSetName = RANDOM_LENGTH_CHARACTER_SET)]
	[Alias("min", "minlength")]
	[ValidateRange(1, MAX_LENGTH - 1)]
	public int MinimumLength { get; set; }

	/// <summary>
	/// Gets or sets the greatest number of characters in each password.
	/// </summary>
	/// <remarks>
	/// The parameter belongs to the <c>RandomLength</c> parameter set and to its twin, <c>RandomLengthCharacterSet</c>.
	/// </remarks>
	/// <value>A length from 2 to 1,048,576 that isn't less than <see cref="MinimumLength"/>.</value>
	[Parameter(Mandatory = true, ParameterSetName = RANDOM_LENGTH)]
	[Parameter(Mandatory = true, ParameterSetName = RANDOM_LENGTH_CHARACTER_SET)]
	[Alias("max", "maxlength")]
	[ValidateRange(2, MAX_LENGTH)]
	public int MaximumLength { get; set; }

	/// <summary>
	/// Gets or sets the groups of characters that passwords are built from, in place of the default groups.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Each value is one of nine strings, which the parameter accepts regardless of case and uses as typed: the
	/// lowercase letters, the lowercase letters except l and o, the uppercase letters, the uppercase letters except I
	/// and O, the digits, the digits except 1, the digits except 0 and 1, the symbols !@$#%&amp;, and the symbols
	/// !#%&amp;. A group that's listed twice supplies two characters to every password that has room for them.
	/// </para>
	/// <para>
	/// Without the parameter or <see cref="CharacterSet"/>, passwords are built from the lowercase letters, the uppercase
	/// letters, the digits, and the symbols !@$#%&amp;. The parameter belongs to the <c>StaticLength</c> and
	/// <c>RandomLength</c> parameter sets, so it can't be used with <see cref="CharacterSet"/>.
	/// <see cref="OnlyUseExtraCharacters"/> leaves out these groups or the default ones.
	/// </para>
	/// </remarks>
	/// <value>The groups of characters, or <see langword="null"/> when the parameter isn't given.</value>
	[Parameter(ParameterSetName = STATIC_LENGTH)]
	[Parameter(ParameterSetName = RANDOM_LENGTH)]
	[Alias("strings")]
	[ValidateSet(
		CharacterSets.Lowercase,
		CharacterSets.LowercaseExceptLAndO,
		CharacterSets.Uppercase,
		CharacterSets.UppercaseExceptIAndO,
		CharacterSets.Digits,
		CharacterSets.DigitsExceptOne,
		CharacterSets.DigitsExceptZeroAndOne,
		CharacterSets.Symbols,
		CharacterSets.SymbolsExceptAtAndDollar)]
	public string[]? InputStrings { get; set; }

	/// <summary>
	/// Gets or sets the named groups of characters that passwords are built from, in place of the default groups.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Each value is a group of characters, and the values combine.
	/// <see cref="MG.Passwords.CharacterSet.NoLookAlikes"/> stands for three groups that leave out characters that are
	/// easy to mistake for others, and it takes the place of <see cref="MG.Passwords.CharacterSet.Lowercase"/>,
	/// <see cref="MG.Passwords.CharacterSet.Uppercase"/>, and <see cref="MG.Passwords.CharacterSet.Digits"/>. The
	/// parameter refuses 0, and any bit that no value defines.
	/// </para>
	/// <para>
	/// The parameter is mandatory in the <c>StaticLengthCharacterSet</c> and <c>RandomLengthCharacterSet</c> parameter
	/// sets, and it belongs to no other, so it can't be used with <see cref="InputStrings"/>.
	/// <see cref="OnlyUseExtraCharacters"/> leaves out these groups too.
	/// </para>
	/// </remarks>
	/// <value>One or more of the values. When the parameter isn't given, the property is 0, which no value is.</value>
	[Parameter(Mandatory = true, ParameterSetName = STATIC_LENGTH_CHARACTER_SET)]
	[Parameter(Mandatory = true, ParameterSetName = RANDOM_LENGTH_CHARACTER_SET)]
	[ValidateFlags]
	public CharacterSet CharacterSet { get; set; }

	/// <summary>
	/// Gets or sets more groups of characters that passwords are built from.
	/// </summary>
	/// <remarks>
	/// A string is one group, as is a <see cref="char"/> or a <see cref="char"/> array, and each element of a collection,
	/// such as each line that <c>Get-Content</c> outputs, is a group of its own. A group can't be empty or hold a control
	/// character or a surrogate.
	/// </remarks>
	/// <value>The groups of characters, or <see langword="null"/> when the parameter isn't given.</value>
	[Parameter]
	[Alias("chars", "ec")]
	[CharacterGroupsTransformation]
	[ValidatePasswordCharacters]
	public string[]? ExtraCharacters { get; set; }

	/// <summary>
	/// Gets or sets a value that indicates whether passwords are built from <see cref="ExtraCharacters"/> alone.
	/// </summary>
	/// <remarks>
	/// When the switch is present, <see cref="CharacterSet"/>, <see cref="InputStrings"/>, and the default groups are left
	/// out, and <see cref="ExtraCharacters"/> is required.
	/// </remarks>
	/// <value>A switch that is present to build passwords from <see cref="ExtraCharacters"/> alone.</value>
	[Parameter]
	[Alias("onlyec")]
	public SwitchParameter OnlyUseExtraCharacters { get; set; }

	/// <summary>
	/// Gets or sets the character that starts every password.
	/// </summary>
	/// <remarks>
	/// The character counts toward the length of each password, and it doesn't come from any group. A bare integer from
	/// 0 to 9, as in <c>-FirstCharacter 5</c>, is read as that digit. The character can't be a control character or a
	/// surrogate.
	/// </remarks>
	/// <value>
	/// The first character of every password. When the parameter isn't given, the property is U+0000, and any character
	/// of the groups can start a password.
	/// </value>
	[Parameter]
	[Alias("fc", "FirstChar")]
	[FirstCharacterTransformation]
	[ValidatePasswordCharacters]
	public char FirstCharacter { get; set; }

	/// <summary>
	/// Gets a value that indicates whether each password gets a random length of its own.
	/// </summary>
	/// <value>
	/// <see langword="true"/> in the <c>RandomLength</c> and <c>RandomLengthCharacterSet</c> parameter sets; otherwise,
	/// <see langword="false"/>.
	/// </value>
	private bool IsRandomLength => this.ParameterSetName is RANDOM_LENGTH or RANDOM_LENGTH_CHARACTER_SET;

	/// <summary>
	/// Checks the arguments that depend on each other, and prepares the groups of characters that every password of the
	/// call is built from.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The method ends the statement with a terminating error, before any password is generated, when
	/// <see cref="MinimumLength"/> is greater than <see cref="MaximumLength"/>
	/// (<c>MinimumLengthExceedsMaximumLength</c>), when <see cref="OnlyUseExtraCharacters"/> is present without
	/// <see cref="ExtraCharacters"/> (<c>OnlyUseExtraCharactersWithoutExtraCharacters</c>), or when no group of
	/// characters is left to draw from (<c>NoCharacters</c>).
	/// </para>
	/// <para>
	/// The groups are those of <see cref="CharacterSet"/> or <see cref="InputStrings"/>, or the default groups without
	/// either, unless <see cref="OnlyUseExtraCharacters"/> is present, followed by those of <see cref="ExtraCharacters"/>.
	/// </para>
	/// </remarks>
	protected override void BeginProcessing()
	{
		if (this.IsRandomLength && this.MinimumLength > this.MaximumLength)
		{
			this.ThrowTerminatingError(new ErrorRecord(
				new ArgumentException(
					$"The minimum length, {this.MinimumLength}, is greater than the maximum length, {this.MaximumLength}."),
				"MinimumLengthExceedsMaximumLength",
				ErrorCategory.InvalidArgument,
				this.MinimumLength));
		}

		// PowerShell refuses a null argument for -InputStrings and -ExtraCharacters, so either one is null only when it
		// isn't bound.
		if (this.OnlyUseExtraCharacters.IsPresent && this.ExtraCharacters is null)
		{
			this.ThrowTerminatingError(new ErrorRecord(
				new ArgumentException("-OnlyUseExtraCharacters needs -ExtraCharacters to supply the characters."),
				"OnlyUseExtraCharactersWithoutExtraCharacters",
				ErrorCategory.InvalidArgument,
				targetObject: null));
		}

		List<string> groups = [];
		if (!this.OnlyUseExtraCharacters.IsPresent)
		{
			if (this.MyInvocation.BoundParameters.ContainsKey(nameof(this.CharacterSet)))
			{
				AddCharacterSetGroups(groups, this.CharacterSet);
			}
			else
			{
				groups.AddRange(this.InputStrings ?? CharacterSets.Default);
			}
		}

		if (this.ExtraCharacters is not null)
		{
			groups.AddRange(this.ExtraCharacters);
		}

		if (groups.Count == 0)
		{
			this.ThrowTerminatingError(new ErrorRecord(
				new ArgumentException("There are no groups of characters to build passwords from."),
				"NoCharacters",
				ErrorCategory.InvalidArgument,
				targetObject: null));
		}

		char? firstCharacter = this.MyInvocation.BoundParameters.ContainsKey(nameof(this.FirstCharacter))
			? this.FirstCharacter
			: null;

		_generator = new PasswordGenerator(groups, firstCharacter);
	}

	/// <summary>
	/// Writes the passwords to the pipeline, one at a time.
	/// </summary>
	/// <remarks>
	/// The method generates each password right before it writes it, so a command later in the pipeline that stops the
	/// pipeline early, such as <c>Select-Object -First</c>, stops the generating too.
	/// </remarks>
	protected override void ProcessRecord()
	{
		// BeginProcessing sets the generator or ends the statement.
		PasswordGenerator generator = _generator!;
		bool randomLength = this.IsRandomLength;
		for (int i = 0; i < this.Count; i++)
		{
			string password = randomLength
				? generator.Generate(this.MinimumLength, this.MaximumLength)
				: generator.Generate(this.PasswordLength);

			this.WriteObject(password);
		}
	}

	/// <summary>
	/// Adds the groups of characters that the specified character sets stand for to the specified list.
	/// </summary>
	/// <remarks>
	/// <see cref="MG.Passwords.CharacterSet.NoLookAlikes"/> adds the lowercase letters except l and o, the uppercase
	/// letters except I and O, and the digits except 0 and 1, in place of the groups of
	/// <see cref="MG.Passwords.CharacterSet.Lowercase"/>, <see cref="MG.Passwords.CharacterSet.Uppercase"/>, and
	/// <see cref="MG.Passwords.CharacterSet.Digits"/>. The groups follow the order of the default groups.
	/// </remarks>
	/// <param name="groups">The list that the groups are added to.</param>
	/// <param name="sets">One or more character sets.</param>
	private static void AddCharacterSetGroups(List<string> groups, CharacterSet sets)
	{
		if (sets.HasFlag(CharacterSet.NoLookAlikes))
		{
			groups.Add(CharacterSets.LowercaseExceptLAndO);
			groups.Add(CharacterSets.UppercaseExceptIAndO);
			groups.Add(CharacterSets.DigitsExceptZeroAndOne);
		}
		else
		{
			if (sets.HasFlag(CharacterSet.Lowercase))
			{
				groups.Add(CharacterSets.Lowercase);
			}

			if (sets.HasFlag(CharacterSet.Uppercase))
			{
				groups.Add(CharacterSets.Uppercase);
			}

			if (sets.HasFlag(CharacterSet.Digits))
			{
				groups.Add(CharacterSets.Digits);
			}
		}

		if (sets.HasFlag(CharacterSet.Symbols))
		{
			groups.Add(CharacterSets.Symbols);
		}
	}
}
