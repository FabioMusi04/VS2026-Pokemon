namespace VisualStudioPokemon.Services
{
    internal enum PokemonAnimationState
    {
        Idle,
        WalkRight,
        WalkLeft,
        Swipe
    }

    internal interface IPokemonSpriteView
    {
        void SetAnimation(PokemonAnimationState state);
    }
}
