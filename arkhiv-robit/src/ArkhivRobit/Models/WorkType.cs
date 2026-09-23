using System.ComponentModel.DataAnnotations;

namespace ArkhivRobit.Models;

/// <summary>Тип студентської роботи.</summary>
public enum WorkType
{
    [Display(Name = "Курсова робота")]
    Coursework = 1,

    [Display(Name = "Дипломна робота")]
    Diploma = 2,
}

public static class WorkTypeExtensions
{
    public static string Title(this WorkType type) => type switch
    {
        WorkType.Coursework => "Курсова робота",
        WorkType.Diploma => "Дипломна робота",
        _ => type.ToString(),
    };
}
