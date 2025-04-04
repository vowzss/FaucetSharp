﻿namespace veloce.shared.exceptions;

public sealed class SessionNotFoundException() : Exception("Session was not found!")
{
}

public sealed class SessionCreationException() : Exception("Session creation failed!")
{
}