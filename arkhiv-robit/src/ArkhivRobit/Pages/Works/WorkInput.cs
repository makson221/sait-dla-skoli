using System.ComponentModel.DataAnnotations;
using ArkhivRobit.Models;

namespace ArkhivRobit.Pages.Works;

/// <summary>Дані форми додавання та редагування роботи.</summary>
public class WorkInput
{
    [Display(Name = "Тип роботи")]
    public WorkType Type { get; set; } = WorkType.Coursework;

    [Required(ErrorMessage = "Вкажіть тему роботи.")]
    [StringLength(300, MinimumLength = 3, ErrorMessage = "Тема має містити від 3 до 300 символів.")]
    [Display(Name = "Тема роботи")]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Вкажіть ПІБ студента.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "ПІБ має містити від 2 до 200 символів.")]
    [Display(Name = "ПІБ студента")]
    public string StudentName { get; set; } = "";

    [Required(ErrorMessage = "Вкажіть групу.")]
    [StringLength(50, ErrorMessage = "Назва групи задовга.")]
    [Display(Name = "Група")]
    public string GroupName { get; set; } = "";

    [Range(1990, 2100, ErrorMessage = "Вкажіть коректний рік.")]
    [Display(Name = "Рік захисту")]
    public int Year { get; set; } = DateTime.Now.Year;

    [StringLength(200)]
    [Display(Name = "Науковий керівник")]
    public string? Supervisor { get; set; }

    [StringLength(4000)]
    [Display(Name = "Анотація")]
    public string? Description { get; set; }

    [Display(Name = "Файл роботи")]
    public IFormFile? File { get; set; }

    public static WorkInput From(Work work) => new()
    {
        Type = work.Type,
        Title = work.Title,
        StudentName = work.StudentName,
        GroupName = work.GroupName,
        Year = work.Year,
        Supervisor = work.Supervisor,
        Description = work.Description,
    };
}
