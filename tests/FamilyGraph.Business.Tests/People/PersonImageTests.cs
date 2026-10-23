using FamilyGraph.Business.People;

namespace FamilyGraph.Business.Tests.People;

public class PersonImageTests
{
    private static readonly AccountId Alice = new(Guid.NewGuid());
    private static readonly AccountId Bob = new(Guid.NewGuid());
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 9, 0, 0, TimeSpan.Zero);

    internal static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];
    internal static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];
    internal static readonly byte[] WebP = [.. "RIFF"u8, 0x10, 0x00, 0x00, 0x00, .. "WEBPVP8 "u8];

    private static Person NewPerson()
    {
        var person = Person.Create(new PersonName("Maya"), Alice);
        person.TakeEvents();
        return person;
    }

    [Fact]
    [Trait("AC", "AC-01.18")]
    public void Maintainer_sets_an_image_and_the_change_is_audited_without_it()
    {
        var person = NewPerson();

        var image = person.SetImage(Jpeg, Alice);

        Assert.Equal((image.Id, person.Id, ImageFormat.Jpeg, Jpeg), (person.ImageId!.Value, image.PersonId, image.Format, image.Content));
        Assert.Equal([new PersonImageChanged(person.Id, Alice)], person.PendingEvents);
    }

    [Fact]
    [Trait("AC", "AC-01.18")]
    public void Replacing_the_image_gives_it_a_new_id()
    {
        var person = NewPerson();
        var first = person.SetImage(Jpeg, Alice);

        var second = person.SetImage(Png, Alice);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(second.Id, person.ImageId);
    }

    [Fact]
    [Trait("AC", "AC-01.18")]
    public void Someone_other_than_the_maintainer_cannot_set_or_remove_the_image()
    {
        var person = NewPerson();
        person.SetImage(Jpeg, Alice);
        person.TakeEvents();

        Assert.Equal("person.not_maintainer", Assert.Throws<NotProfileMaintainerException>(() => person.SetImage(Png, Bob)).Code);
        Assert.Throws<NotProfileMaintainerException>(() => person.RemoveImage(Bob));
        Assert.Empty(person.PendingEvents);
    }

    [Fact]
    [Trait("AC", "AC-01.18")]
    public void Maintainer_removes_the_image()
    {
        var person = NewPerson();
        person.SetImage(Jpeg, Alice);
        person.TakeEvents();

        person.RemoveImage(Alice);

        Assert.Null(person.ImageId);
        Assert.Equal([new PersonImageRemoved(person.Id, Alice)], person.PendingEvents);
    }

    [Fact]
    public void Removing_a_missing_image_is_a_conflict()
    {
        var person = NewPerson();

        Assert.Equal("person.no_image", Assert.Throws<ProfileHasNoImageException>(() => person.RemoveImage(Alice)).Code);
    }

    [Theory]
    [Trait("AC", "AC-01.19")]
    [InlineData("Jpeg")]
    [InlineData("Png")]
    [InlineData("WebP")]
    public void Jpeg_png_and_webp_are_recognized_by_their_signature(string format)
    {
        var content = format switch { "Jpeg" => Jpeg, "Png" => Png, _ => WebP };

        Assert.Equal(ImageFormat.FromName(format), NewPerson().SetImage(content, Alice).Format);
    }

    [Theory]
    [Trait("AC", "AC-01.19")]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 })]
    [InlineData(new byte[] { 0x3C, 0x73, 0x76, 0x67 })]
    [InlineData(new byte[] { 0xFF, 0xD8 })]
    public void Other_content_is_refused(byte[] content)
    {
        var person = NewPerson();

        var error = Assert.Throws<UnsupportedImageFormatException>(() => person.SetImage(content, Alice));

        Assert.Equal("profile_image.unsupported_format", error.Code);
        Assert.Null(person.ImageId);
    }

    [Fact]
    [Trait("AC", "AC-01.19")]
    public void An_image_over_one_mebibyte_is_refused_and_one_at_the_limit_is_accepted()
    {
        var atLimit = new byte[ProfileImage.MaxBytes];
        Jpeg.CopyTo(atLimit, 0);
        var overLimit = new byte[ProfileImage.MaxBytes + 1];
        Jpeg.CopyTo(overLimit, 0);
        var person = NewPerson();

        Assert.Equal("profile_image.too_large", Assert.Throws<ProfileImageTooLargeException>(() => person.SetImage(overLimit, Alice)).Code);
        Assert.Equal(ProfileImage.MaxBytes, person.SetImage(atLimit, Alice).Content.Length);
    }

    [Fact]
    [Trait("AC", "AC-05.13")]
    public void Deleting_the_profile_clears_its_image_and_a_deleted_profile_takes_no_image()
    {
        var person = NewPerson();
        person.SetImage(Jpeg, Alice);

        person.Delete(Alice, actorIsAdministrator: false, Now);

        Assert.Null(person.ImageId);
        Assert.Equal("person.deleted", Assert.Throws<ProfileDeletedException>(() => person.SetImage(Jpeg, Alice)).Code);
        Assert.Throws<ProfileDeletedException>(() => person.RemoveImage(Alice));
    }
}
