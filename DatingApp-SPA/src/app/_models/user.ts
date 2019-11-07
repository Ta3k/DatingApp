import { Photo } from './photo';

export interface User {
    id: number;
    username: string;
    knownAs: string;
    age: number;
    gender: string;
    created: Date;
    lastAvtive: Date;
    photoUrl: string;
    city: string;
    country: string;
    interests?: string;
    intruduction?: string;
    lookingFor?: string;
    photos?: Photo[];
}
