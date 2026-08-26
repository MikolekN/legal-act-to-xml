using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Saga.Core.Tests
{
	/// <summary>
	/// Strażnik mechanizmu pomijania: test sięgający po dokument z DocRepo/ nie może być zwykłym
	/// [Fact]/[Theory], bo na czystym klonie — bez niewersjonowanych danych — wywali cały przebieg
	/// zamiast pominąć się z powodem.
	///
	/// Reguła działa na poziomie PLIKU, nie metody: droga do DocRepo/ prowadzi zwykle przez prywatny
	/// helper klasy testowej, więc przypisanie zależności do konkretnej metody wymagałoby analizy
	/// składniowej, a nie dałoby nic ponad tę prostszą regułę.
	/// </summary>
	public class DocRepoFactGuardTests
	{
		// [Fact] albo [Theory] BEZ prefiksu — „[DocRepoFact(” się nie łapie, bo po nawiasie
		// kwadratowym musi od razu stać nazwa atrybutu.
		private static readonly Regex BareTestAttribute =
			new(@"\[\s*(?:Fact|Theory)\s*[\](]", RegexOptions.Compiled);

		// TestFiles.cs definiuje samą metodę, a ten plik cytuje jej nazwę w literale poniżej —
		// oba wspominają ją bez zależności od danych.
		private static readonly string[] Exempt = { "TestFiles.cs", "DocRepoFactGuardTests.cs" };

		[Fact]
		public void TestsUsingReferenceDocs_AreMarkedWithDocRepoFact()
		{
			var offenders = new List<string>();

			foreach (var path in Directory.EnumerateFiles(TestFiles.GetTestProjectRoot(), "*.cs", SearchOption.AllDirectories))
			{
				var separator = Path.DirectorySeparatorChar;
				if (path.Contains($"{separator}bin{separator}") || path.Contains($"{separator}obj{separator}"))
				{
					continue;
				}

				var fileName = Path.GetFileName(path);
				if (Array.IndexOf(Exempt, fileName) >= 0)
				{
					continue;
				}

				var source = File.ReadAllText(path);
				if (!source.Contains("GetReferenceDocPath("))
				{
					continue;
				}

				foreach (Match match in BareTestAttribute.Matches(source))
				{
					offenders.Add($"{fileName}: {match.Value}");
				}
			}

			Assert.True(offenders.Count == 0,
				"Test korzystający z niewersjonowanego DocRepo/ musi być oznaczony [DocRepoFact(...)] — "
				+ "zwykły [Fact]/[Theory] przy braku lokalnych dokumentów czerwieni cały przebieg zamiast "
				+ "pominąć test. Znalezione: " + string.Join(", ", offenders));
		}
	}
}
