using Connect4HoopsArcade.Core.Narration;
using Xunit;

namespace Connect4HoopsArcade.Core.Tests;

public class VoicePickerTests
{
    [Fact]
    public void Single_item_always_returns_zero()
    {
        Assert.Equal(0, VoicePicker.Pick(count: 1, lastIndex: 0, roll: 99));
        Assert.Equal(0, VoicePicker.Pick(count: 1, lastIndex: -1, roll: 0));
    }

    [Fact]
    public void Never_repeats_the_last_index()
    {
        for (int roll = 0; roll < 100; roll++)
            Assert.NotEqual(1, VoicePicker.Pick(count: 3, lastIndex: 1, roll: roll));
    }

    [Fact]
    public void First_pick_with_no_history_uses_full_range()
    {
        Assert.Equal(2, VoicePicker.Pick(count: 3, lastIndex: -1, roll: 2));
        Assert.Equal(0, VoicePicker.Pick(count: 3, lastIndex: -1, roll: 3)); // wraps
    }

    [Fact]
    public void Result_is_always_in_range_and_not_last()
    {
        for (int roll = -5; roll < 50; roll++)
        {
            int r = VoicePicker.Pick(count: 4, lastIndex: 2, roll: roll);
            Assert.InRange(r, 0, 3);
            Assert.NotEqual(2, r);
        }
    }
}
